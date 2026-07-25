# Phase 8 verification

Date: 2026-07-25
Environment: Windows, .NET SDK 10.0, no physical PLC or serial hardware attached

## Automated evidence

- `dotnet build Wombat.IndustrialCommunication.sln --no-restore`: both `netstandard2.0` and `net10.0` library targets compile.
- `dotnet test Wombat.IndustrialCommunicationTestProject/Wombat.IndustrialCommunicationTestProject.csproj --no-restore --filter "FullyQualifiedName~ChannelTests|FullyQualifiedName~ServerTests|FullyQualifiedName~TransportTests|FullyQualifiedName~ModbusBluetoothClientTests"`: 39 passed, 0 failed, 0 skipped.
- The unfiltered test run did not finish within 60 seconds. Tests that use fixed private-network PLC addresses are not consistently classified, so this is not recorded as a pass.

## Hardware evidence required

The following acceptance evidence cannot be produced without configured hardware and must not be treated as passed:

- S7 Channel execution and recovery against a real PLC.
- FINS Channel execution and recovery against a real PLC.
- Modbus TCP/RTU multi-station framing and PDU limits against real devices.

Record device model, endpoint/serial settings, UTC start/end time, test command, and result when each run is performed. Credentials and raw production payloads must not be stored in this file.

## Remaining software gate

The phase 8 sync-over-async scan still reports legacy synchronous client/server APIs. They require removal together with their public interfaces and synchronous tests; replacing only their internals would preserve the deadlock-prone contract. Until that breaking migration and the hardware runs above are complete, phase 8 is not closed.
