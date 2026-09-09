# Modbus/S7 Communication Reliability Remediation Plan

## Scope and outcome

This plan covers the actual Modbus TCP, Modbus RTU, and Siemens S7 client/channel code only. It fixes response framing, request-response serialization, cancellation, fault classification, disposal, and channel shutdown behavior.

The work deliberately does not add a new connection-pool model, a background reconnect service, or a task scheduler. `ChannelManager` remains the lifecycle owner and `SemaphoreSlim`/the existing `AsyncLock` remain the only synchronization primitives.

The two phases must be completed and tested in order. Phase 2 depends on Phase 1 because a channel cannot stop or recover reliably until protocol I/O has correct framing, cancellation, and error classification.

## Non-goals

- Do not change synchronous API behavior in this work. New cancellation-aware overloads are additive; existing overloads forward with `CancellationToken.None`.
- Do not automatically retry a write after the request has been sent. A missing or invalid post-send response remains `OperationFailureKind.OutcomeUnknown`.
- Do not add TLS to Modbus/S7 sockets in this change. These protocols are normally plaintext and most PLCs do not support TLS. Production deployments must enforce PLC-network segmentation and ACLs outside this library.
- Do not modify FINS, Bluetooth, or server behavior except where a shared transport signature must remain source-compatible.

## Phase 1: Protocol transport correctness and client resource ownership

### Objective

Make one Modbus/S7 protocol operation produce exactly one validated response or one structured failure. A deterministic Modbus exception response must never become a receive timeout, and concurrent callers must never consume each other's response.

### 1. Replace fixed Modbus response lengths with framed reads

#### Objects to change

- `Wombat.IndustrialCommunication/Messaging/DeviceMessageTransport.cs`
- `Wombat.IndustrialCommunication/Modbus/Base/ModbusTcpRequest.cs`
- `Wombat.IndustrialCommunication/Modbus/Base/ModbusRTURequest.cs`
- `Wombat.IndustrialCommunication/Modbus/ModbusTcpClientBase.cs`
- `Wombat.IndustrialCommunication/Modbus/ModbusRTUClientBase.cs`

#### Interface decision

Keep `IDeviceMessageTransport` unchanged. The existing public methods remain source-compatible:

```csharp
Task<OperationResult<IDeviceReadWriteMessage>> UnicastReadMessageAsync(
    IDeviceReadWriteMessage request,
    CancellationToken cancellationToken);

Task<OperationResult<IDeviceReadWriteMessage>> UnicastWriteMessageAsync(
    IDeviceReadWriteMessage request,
    CancellationToken cancellationToken);
```

Add protected helpers to `DeviceMessageTransport`, not another public transport interface:

```csharp
protected Task<OperationResult<byte[]>> ReceiveExactAsync(int length, CancellationToken cancellationToken);
protected Task<OperationResult<IDeviceReadWriteMessage>> ExchangeModbusTcpAsync(
    ModbusTcpRequest request, bool isWrite, CancellationToken cancellationToken);
protected Task<OperationResult<IDeviceReadWriteMessage>> ExchangeModbusRtuAsync(
    ModbusRTURequest request, bool isWrite, CancellationToken cancellationToken);
```

The exact names may differ, but the responsibilities must remain in `DeviceMessageTransport`; `ModbusTcpClientBase` and `ModbusRTUClientBase` must not implement duplicate socket frame readers.

#### Required behavior

1. Replace the separate `_asyncLock` send and receive critical sections with one request-response gate. The gate starts before the request write and is released only after the entire response has been read, validated, or classified as failed.
2. For `ModbusTcpRequest`:
   - Send the request under that gate.
   - Read the six-byte MBAP prefix first.
   - Decode the MBAP length as big-endian; reject zero, invalid protocol id, and values above the Modbus TCP ADU limit before allocating.
   - Read exactly the declared remaining bytes, including Unit ID and PDU.
   - Treat an exception PDU (`functionCode | 0x80`, one exception byte) as a complete response. Return `DeviceRejected`, not a timeout.
3. For `ModbusRTURequest`:
   - Read station and function first.
   - For exception functions, read exception-code plus CRC only, giving a five-byte ADU.
   - For functions `0x01` to `0x04`, read the byte-count field, then data plus CRC; enforce the protocol maximum before allocating.
   - For `0x05`, `0x06`, `0x0F`, and `0x10`, read the fixed address/quantity echo plus CRC.
   - Validate CRC before parsing data.
4. Stop using `IDeviceReadWriteMessage.ProtocolResponseLength` to determine Modbus response size. Keep the property temporarily for interface compatibility and S7 requests, but remove its use from Modbus send/receive code.
5. Preserve the request and response hex traces in every failure result. Preserve `Exception` and `FailureKind` when wrapping an `OperationResult`.

### 2. Validate the response against the originating Modbus request

#### Objects to change

- `Wombat.IndustrialCommunication/Modbus/ModbusTcpClientBase.cs`
- `Wombat.IndustrialCommunication/Modbus/ModbusRTUClientBase.cs`
- `Wombat.IndustrialCommunication/Modbus/Base/ModbusTcpResponse.cs`
- `Wombat.IndustrialCommunication/Modbus/Base/ModbusRTUResponse.cs`

#### Required validation

Create private validation methods in the two client base classes; do not add a validator framework.

```csharp
private static OperationResult<IDeviceReadWriteMessage> ValidateTcpResponse(
    ModbusTcpRequest request, OperationResult<IDeviceReadWriteMessage> response);

private static OperationResult<IDeviceReadWriteMessage> ValidateRtuResponse(
    ModbusRTURequest request, OperationResult<IDeviceReadWriteMessage> response);
```

Validation rules:

- TCP: transaction id, protocol id, Unit ID, function code, declared MBAP length, and read byte-count must match the request.
- RTU: CRC, station, function code, and read byte-count must match the request.
- Writes: validate the function-specific echoed address and written value/quantity for `0x05`, `0x06`, `0x0F`, and `0x10`.
- A matching Modbus exception response maps to `DeviceRejected`; a mismatched or malformed response maps to `ProtocolViolation`.
- A read transport failure keeps its transport failure kind. A write failure after `SendRequestAsync` succeeded is normalized to `OutcomeUnknown`, except a valid Modbus exception response, which is `DeviceRejected`.

`ModbusTcpResponse.Initialize` must parse MBAP multibyte fields explicitly as big-endian instead of `BitConverter.ToUInt16`, so public properties remain correct on little-endian machines.

### 3. Route every direct Modbus operation through the same exchange boundary

#### Objects to change

- `Wombat.IndustrialCommunication/Modbus/ModbusTcpClient.cs`
- `Wombat.IndustrialCommunication/Modbus/ModbusRtuClient.cs`
- `Wombat.IndustrialCommunication/Modbus/ModbusTcpClientBase.cs`
- `Wombat.IndustrialCommunication/Modbus/ModbusRTUClientBase.cs`

#### Required behavior

1. Ensure `Read*Async`, `Write*Async`, `BatchReadAsync`, and `BatchWriteAsync` all pass through the base-class request-response gate.
2. Remove the separate direct-write route that currently reaches `WriteByModbusAddressAsync` without the base operation lock.
3. Keep default `ChannelOptions.MaxConcurrency = 1`. When callers set it higher, the client still serializes Modbus TCP and RTU exchanges; increasing channel concurrency may queue work but must not permit concurrent frames on one physical connection.
4. Preserve the existing public synchronous APIs as wrappers only. Do not use a new `Task.Run` wrapper.

### 4. Make adapter and protocol failures structured and disposable

#### Objects to change

- `Wombat.IndustrialCommunication/Adapters/TcpClientAdapter.cs`
- `Wombat.IndustrialCommunication/Adapters/SerialPortAdapter.cs`
- `Wombat.IndustrialCommunication/Messaging/DeviceMessageTransport.cs`
- `Wombat.IndustrialCommunication/Core/DeviceDataReaderWriterBase.cs`
- `Wombat.IndustrialCommunication/PLC/S7/S7EthernetTransport.cs`

#### Required behavior

1. `SerialPortAdapter.Send` and `Receive` must map cancellation, timeout, closed-port, and I/O failures to `Cancelled`, `SendTimeout`/`ReceiveTimeout`, `ConnectionClosed`, and `TransportFailure` respectively. They must retain the originating exception.
2. After a serial transport timeout, close the port before returning so a partial/stale RTU response cannot be consumed by the next request.
3. In `S7EthernetTransport`, return the populated failure `result` from all catch paths. Do not replace it with an empty `CreateFailedResult` result. Set `ProtocolViolation` for malformed TPKT/S7 frames; preserve transport and cancellation kinds from `DeviceMessageTransport`.
4. Update `S7EthernetTransport.ReceiveFullResponseAsync` to validate TPKT version/reserved bytes and its declared total length before reading the payload. Reject invalid lengths as `ProtocolViolation`.
5. `DeviceDataReaderWriterBase.Dispose(bool)` must dispose its owned `DeviceMessageTransport`. `DeviceMessageTransport.Dispose` must dispose its `IStreamResource` exactly once.
6. `TcpClientAdapter.Dispose(bool)` must close/dispose the current `TcpTransportConnection`, not merely clear its reference. `Dispose`, `DisconnectAsync`, and `StreamClose` must be idempotent and serialized through `_lifecycleGate`.
7. Add equivalent idempotent close behavior to `SerialPortAdapter.Dispose`; avoid closing a stream concurrently with an operation without first signaling/serializing lifecycle state.

### 5. Phase 1 tests

Add deterministic loopback/fake-stream tests; no real PLC or COM port is required.

- `ModbusMessageFrameConsistencyTests`:
  - TCP and RTU exception frames are consumed at their actual length and return `DeviceRejected`.
  - Mismatched Unit ID, station, function, byte count, and write echo return `ProtocolViolation`.
  - MBAP fields are parsed big-endian.
  - A write sent successfully with a missing response returns `OutcomeUnknown`.
- Add a focused `DeviceMessageTransport` test file:
  - Two simultaneous Modbus operations cannot interleave send/receive.
  - A malformed length is rejected before a large allocation.
- `TcpClientAdapterTimeoutTests` and `SerialPortAdapterLifecycleTests`:
  - cancellation/timeout preserves the correct `FailureKind`;
  - `Dispose` closes the active resource;
  - repeated `DisconnectAsync` and `Dispose` are safe.
- S7 tests:
  - invalid TPKT header/length and cancellation retain `ProtocolViolation`/`Cancelled` rather than `None`.

#### Phase 1 exit criteria

1. Modbus exception responses never produce a receive timeout merely because they are shorter than the success response.
2. No Modbus exchange can have another caller consume its response.
3. A direct Modbus/S7 client releases its transport after `Dispose`.
4. Every adapter and S7 transport failure reaching a channel has a non-`None` `FailureKind`.
5. The targeted tests pass without real hardware.

## Phase 2: Channel configuration snapshot, cancellation propagation, and shutdown

### Objective

Make a registered channel's concurrency, timeout, and reconnect behavior immutable for its lifetime, and ensure a Modbus/S7 operation started through `ChannelManager` observes the manager's cancellation token through protocol I/O.

### 1. Snapshot channel configuration at registration

#### Objects to change

- `Wombat.IndustrialCommunication/Channels/ChannelModels.cs`
- `Wombat.IndustrialCommunication/Channels/ChannelManager.cs`
- `Wombat.IndustrialCommunication/Channels/ChannelRuntime.cs`

#### Design

Do not make a new public configuration API. Keep mutable `ChannelOptions` for caller construction compatibility, but create one internal immutable snapshot when `AddAsync` succeeds validation.

Add an internal sealed `ChannelRuntimeOptions` in `ChannelModels.cs` (or an internal nested type in `ChannelRuntime.cs`) containing copied scalar values:

```csharp
internal sealed class ChannelRuntimeOptions
{
    internal string Id { get; }
    internal int MaxConcurrency { get; }
    internal TimeSpan ConnectTimeout { get; }
    internal TimeSpan OperationTimeout { get; }
    internal TimeSpan ReconnectInitialDelay { get; }
    internal TimeSpan ReconnectMaxDelay { get; }
    internal int ReconnectMaxAttempts { get; }
}
```

`ChannelManager.AddAsync` creates the snapshot after validation, creates the protocol client using that same snapshot, and passes the snapshot to `ChannelRuntime`. `ChannelRuntime` must not retain a `ChannelOptions` reference.

The semaphore capacity is stored once in a readonly `_operationCapacity`. `StopAsync` drains and restores exactly `_operationCapacity`, never a mutable option value. Changing a caller-owned `ChannelOptions` object after registration has no effect; `RestartAsync` is the sole way to apply a new configuration.

### 2. Add cancellation-aware Modbus and S7 operation overloads

#### Objects and interfaces to change

- `Wombat.IndustrialCommunication/Modbus/IModbusClient.cs`
- `Wombat.IndustrialCommunication/Modbus/ModbusTcpClient.cs`
- `Wombat.IndustrialCommunication/Modbus/ModbusRtuClient.cs`
- `Wombat.IndustrialCommunication/Core/DeviceDataReaderWriterBase.cs`
- `Wombat.IndustrialCommunication/PLC/S7/SiemensClient.cs`
- `Wombat.IndustrialCommunication/Models/IReadWrite.cs`, only for methods whose implementation now supports the token

Add cancellation-aware overloads for the direct Modbus API, for example:

```csharp
Task<OperationResult<ushort>> ReadHoldingRegisterAsync(
    byte stationNumber, ushort address, CancellationToken cancellationToken);

Task<OperationResult> WriteHoldingRegisterAsync(
    byte stationNumber, ushort address, ushort value, CancellationToken cancellationToken);
```

Add corresponding overloads for all eight direct Modbus read methods and four direct Modbus write methods. Existing interface methods remain and forward to the new overload using `CancellationToken.None`.

In `DeviceDataReaderWriterBase`, add token-taking overloads for typed async read/write methods that currently call the protected `ReadAsync`/`WriteAsync` without a token. Existing methods remain compatibility wrappers.

In `SiemensClient`, ensure all public typed/batch async entry points pass their token through the long-connection path, short-connection connect/disconnect path, and `S7Communication` base operation. In short-connection mode, the connection lifetime must be protected by one operation lock from disconnect through final disconnect; individual lifecycle calls must not interleave with another operation.

### 3. Tighten ChannelRuntime stop and recovery semantics

#### Objects to change

- `Wombat.IndustrialCommunication/Channels/ChannelRuntime.cs`
- `Wombat.IndustrialCommunication/Channels/ChannelManager.cs`

#### Required behavior

1. `ExecuteAsync` continues linking caller cancellation, `_stopping.Token`, and operation timeout, but the supplied operation must invoke a cancellation-aware protocol overload. Update all Modbus/S7 channel examples and tests accordingly.
2. `StopAsync` transitions to `Stopping`, cancels `_stopping`, then waits for exactly `_operationCapacity` permits before `DisconnectAsync`. With Phase 2 operation overloads, active protocol operations release their permit after cancellation.
3. `StopAsync` must remain idempotent under concurrent `RemoveAsync`/`DisposeAsync`. Keep one lifecycle task or a lifecycle gate so all callers await the same stop completion rather than independently draining the semaphore.
4. `RecordResultAsync` and exception handling use the structured kinds produced in Phase 1. `Cancelled` caused by caller cancellation does not fault a channel. `DeviceRejected`, validation, and address failures do not reconnect. `ConnectionClosed`, transport failures, timeouts, protocol violations, and `OutcomeUnknown` fault/disconnect the channel.
5. Do not retry a write in `ChannelRuntime`. The channel only makes the next caller reconnect; it does not replay the failed operation.

### 4. Phase 2 tests

Extend `Wombat.IndustrialCommunicationTestProject/ChannelTests/ChannelManagerTests.cs` and add protocol-specific tests where needed.

- Register a channel with `MaxConcurrency = 1`, mutate the original options to `2`, then verify `RemoveAsync` completes and disconnects once.
- Mutate reconnect and timeout values after registration and verify the runtime still uses the registration snapshot.
- Start a token-aware Modbus/S7 operation that blocks in receive; call `RemoveAsync`; assert the operation is cancelled, the channel reaches `Stopped`, and the connection is closed.
- Start multiple `RemoveAsync`/`DisposeAsync` calls concurrently; assert one disconnect, no semaphore overflow, and all callers complete.
- Run concurrent direct Modbus write calls and concurrent `ChannelManager.ExecuteAsync` calls with `MaxConcurrency > 1`; assert exchanges remain serialized and responses stay associated with their requests.
- Assert `DeviceRejected` preserves `Online`; assert transport/timeout/protocol/unknown-write failures transition to `Faulted` and the next operation performs exactly one shared reconnect.
- Add a regression test proving a caller-owned, token-ignoring delegate is documented as non-cancellable; all library-provided Modbus/S7 delegates must be token-aware.

#### Phase 2 exit criteria

1. Post-registration mutation of `ChannelOptions` cannot deadlock or alter an existing channel.
2. `RemoveAsync`/`DisposeAsync` complete for cancelled Modbus/S7 I/O without relying on a fixed delay.
3. Channel recovery decisions are based only on `OperationFailureKind`, not error text.
4. Concurrent channel shutdown is idempotent and leaves no connected transport or outstanding operation permit.
5. All new tests and the existing Modbus/S7 non-hardware tests pass.

## Delivery order and review gates

1. Implement Phase 1 framing and validation first, run only its targeted tests, then run the Modbus/S7 unit tests that do not require real hardware.
2. Implement Phase 2 only after Phase 1 is green. Run Channel tests plus all Phase 1 tests.
3. Before merge, run `dotnet test Wombat.IndustrialCommunicationTestProject/Wombat.IndustrialCommunicationTestProject.csproj --no-restore` with real-PLC tests explicitly excluded or reported separately. A device-unreachable result is not a passing test.
4. Review the final diff for new background tasks, retry loops, public pool abstractions, and error-message-based recovery. None are permitted by this plan.
