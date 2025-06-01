using System;
using System.Threading.Tasks;

namespace Wombat.IndustrialCommunication.Modbus
{
    /// <summary>
    /// Modbus客户端接口，提供直接的寄存器读写操作
    /// </summary>
    public interface IModbusClient
    {
        #region 同步方法

        /// <summary>
        /// 读取线圈状态 (功能码 01)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="address">起始地址</param>
        /// <returns>线圈状态</returns>
        OperationResult<bool> ReadCoil(byte stationNumber, ushort address);

        /// <summary>
        /// 读取多个线圈状态 (功能码 01)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="count">读取数量</param>
        /// <returns>线圈状态数组</returns>
        OperationResult<bool[]> ReadCoils(byte stationNumber, ushort startAddress, ushort count);

        /// <summary>
        /// 读取离散输入状态 (功能码 02)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="address">起始地址</param>
        /// <returns>离散输入状态</returns>
        OperationResult<bool> ReadDiscreteInput(byte stationNumber, ushort address);

        /// <summary>
        /// 读取多个离散输入状态 (功能码 02)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="count">读取数量</param>
        /// <returns>离散输入状态数组</returns>
        OperationResult<bool[]> ReadDiscreteInputs(byte stationNumber, ushort startAddress, ushort count);

        /// <summary>
        /// 读取保持寄存器 (功能码 03)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="address">起始地址</param>
        /// <returns>保持寄存器值</returns>
        OperationResult<ushort> ReadHoldingRegister(byte stationNumber, ushort address);

        /// <summary>
        /// 读取多个保持寄存器 (功能码 03)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="count">读取数量</param>
        /// <returns>保持寄存器值数组</returns>
        OperationResult<ushort[]> ReadHoldingRegisters(byte stationNumber, ushort startAddress, ushort count);

        /// <summary>
        /// 读取输入寄存器 (功能码 04)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="address">起始地址</param>
        /// <returns>输入寄存器值</returns>
        OperationResult<ushort> ReadInputRegister(byte stationNumber, ushort address);

        /// <summary>
        /// 读取多个输入寄存器 (功能码 04)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="count">读取数量</param>
        /// <returns>输入寄存器值数组</returns>
        OperationResult<ushort[]> ReadInputRegisters(byte stationNumber, ushort startAddress, ushort count);

        /// <summary>
        /// 写入单个线圈 (功能码 05)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="address">线圈地址</param>
        /// <param name="value">写入值</param>
        /// <returns>操作结果</returns>
        OperationResult WriteCoil(byte stationNumber, ushort address, bool value);

        /// <summary>
        /// 写入多个线圈 (功能码 15)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="values">写入值数组</param>
        /// <returns>操作结果</returns>
        OperationResult WriteCoils(byte stationNumber, ushort startAddress, bool[] values);

        /// <summary>
        /// 写入单个保持寄存器 (功能码 06)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入值</param>
        /// <returns>操作结果</returns>
        OperationResult WriteHoldingRegister(byte stationNumber, ushort address, ushort value);

        /// <summary>
        /// 写入多个保持寄存器 (功能码 16)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="values">写入值数组</param>
        /// <returns>操作结果</returns>
        OperationResult WriteHoldingRegisters(byte stationNumber, ushort startAddress, ushort[] values);

        #endregion

        #region 异步方法

        /// <summary>
        /// 异步读取线圈状态 (功能码 01)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="address">起始地址</param>
        /// <returns>线圈状态</returns>
        Task<OperationResult<bool>> ReadCoilAsync(byte stationNumber, ushort address);

        /// <summary>
        /// 异步读取多个线圈状态 (功能码 01)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="count">读取数量</param>
        /// <returns>线圈状态数组</returns>
        Task<OperationResult<bool[]>> ReadCoilsAsync(byte stationNumber, ushort startAddress, ushort count);

        /// <summary>
        /// 异步读取离散输入状态 (功能码 02)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="address">起始地址</param>
        /// <returns>离散输入状态</returns>
        Task<OperationResult<bool>> ReadDiscreteInputAsync(byte stationNumber, ushort address);

        /// <summary>
        /// 异步读取多个离散输入状态 (功能码 02)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="count">读取数量</param>
        /// <returns>离散输入状态数组</returns>
        Task<OperationResult<bool[]>> ReadDiscreteInputsAsync(byte stationNumber, ushort startAddress, ushort count);

        /// <summary>
        /// 异步读取保持寄存器 (功能码 03)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="address">起始地址</param>
        /// <returns>保持寄存器值</returns>
        Task<OperationResult<ushort>> ReadHoldingRegisterAsync(byte stationNumber, ushort address);

        /// <summary>
        /// 异步读取多个保持寄存器 (功能码 03)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="count">读取数量</param>
        /// <returns>保持寄存器值数组</returns>
        Task<OperationResult<ushort[]>> ReadHoldingRegistersAsync(byte stationNumber, ushort startAddress, ushort count);

        /// <summary>
        /// 异步读取输入寄存器 (功能码 04)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="address">起始地址</param>
        /// <returns>输入寄存器值</returns>
        Task<OperationResult<ushort>> ReadInputRegisterAsync(byte stationNumber, ushort address);

        /// <summary>
        /// 异步读取多个输入寄存器 (功能码 04)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="count">读取数量</param>
        /// <returns>输入寄存器值数组</returns>
        Task<OperationResult<ushort[]>> ReadInputRegistersAsync(byte stationNumber, ushort startAddress, ushort count);

        /// <summary>
        /// 异步写入单个线圈 (功能码 05)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="address">线圈地址</param>
        /// <param name="value">写入值</param>
        /// <returns>操作结果</returns>
        Task<OperationResult> WriteCoilAsync(byte stationNumber, ushort address, bool value);

        /// <summary>
        /// 异步写入多个线圈 (功能码 15)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="values">写入值数组</param>
        /// <returns>操作结果</returns>
        Task<OperationResult> WriteCoilsAsync(byte stationNumber, ushort startAddress, bool[] values);

        /// <summary>
        /// 异步写入单个保持寄存器 (功能码 06)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">写入值</param>
        /// <returns>操作结果</returns>
        Task<OperationResult> WriteHoldingRegisterAsync(byte stationNumber, ushort address, ushort value);

        /// <summary>
        /// 异步写入多个保持寄存器 (功能码 16)
        /// </summary>
        /// <param name="stationNumber">站号</param>
        /// <param name="startAddress">起始地址</param>
        /// <param name="values">写入值数组</param>
        /// <returns>操作结果</returns>
        Task<OperationResult> WriteHoldingRegistersAsync(byte stationNumber, ushort startAddress, ushort[] values);

        #endregion
    }
} 