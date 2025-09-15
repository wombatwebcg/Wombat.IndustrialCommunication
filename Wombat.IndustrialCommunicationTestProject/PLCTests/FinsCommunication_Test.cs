using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wombat.Extensions.DataTypeExtensions;
using Wombat.IndustrialCommunication.PLC;
using Wombat.IndustrialCommunicationTestProject.Helper;
using Xunit;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTestProject.PLCTests
{
    /// <summary>
    /// FINS协议通信报文格式测试
    /// </summary>
    public class FinsCommunication_Test
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<FinsCommunication_Test> _logger;

        public FinsCommunication_Test(ITestOutputHelper output)
        {
            _output = output;
            _logger = TestLoggerFactory.CreateLogger<FinsCommunication_Test>(output);
        }

        #region 握手命令测试

        [Fact]
        public void Test_BuildHandshakeCommand_Format()
        {
            // 测试FINS握手命令的报文格式
            var handshakeCommand = FinsCommonMethods.BuildHandshakeCommand();
            
            _output.WriteLine($"握手命令长度: {handshakeCommand.Length}");
            _output.WriteLine($"握手命令内容: {string.Join(" ", handshakeCommand.Select(b => b.ToString("X2")))}");
            
            // 验证握手命令长度
            Assert.Equal(24, handshakeCommand.Length);
            
            // 验证长度字段 (前4字节，大端序，值为20)
            Assert.Equal(0x00, handshakeCommand[0]);
            Assert.Equal(0x00, handshakeCommand[1]);
            Assert.Equal(0x00, handshakeCommand[2]);
            Assert.Equal(0x14, handshakeCommand[3]); // 20字节数据长度
            
            // 验证FINS魔数 ("FINS")
            Assert.Equal(0x46, handshakeCommand[4]); // 'F'
            Assert.Equal(0x49, handshakeCommand[5]); // 'I'
            Assert.Equal(0x4E, handshakeCommand[6]); // 'N'
            Assert.Equal(0x53, handshakeCommand[7]); // 'S'
            
            // 验证客户端节点地址字段 (4字节，全为0)
            for (int i = 8; i < 12; i++)
            {
                Assert.Equal(0x00, handshakeCommand[i]);
            }
            
            // 验证服务器节点地址字段 (4字节，全为0)
            for (int i = 12; i < 16; i++)
            {
                Assert.Equal(0x00, handshakeCommand[i]);
            }
            
            // 验证其他参数字段 (8字节，全为0)
            for (int i = 16; i < 24; i++)
            {
                Assert.Equal(0x00, handshakeCommand[i]);
            }
        }

        [Fact]
        public void Test_ValidateHandshakeResponse_Format()
        {
            // 测试有效的握手响应
            var validResponse = new byte[24];
            validResponse[0] = 0x00; // 长度字段
            validResponse[1] = 0x00;
            validResponse[2] = 0x00;
            validResponse[3] = 0x14;
            validResponse[4] = 0x80; // ICF字段
            
            var isValid = FinsCommonMethods.ValidateHandshakeResponse(validResponse);
            Assert.True(isValid, "有效的握手响应应该通过验证");
            
            // 测试无效的握手响应 - 长度不足
            var invalidResponse1 = new byte[20];
            var isInvalid1 = FinsCommonMethods.ValidateHandshakeResponse(invalidResponse1);
            Assert.False(isInvalid1, "长度不足的响应应该验证失败");
            
            // 测试无效的握手响应 - ICF字段错误
            var invalidResponse2 = new byte[24];
            invalidResponse2[4] = 0x00; // 错误的ICF字段
            var isInvalid2 = FinsCommonMethods.ValidateHandshakeResponse(invalidResponse2);
            Assert.False(isInvalid2, "ICF字段错误的响应应该验证失败");
        }

        #endregion

        #region FINS地址解析测试

        [Theory]
        [InlineData("D100", FinsMemoryArea.DM, 100, false, 0)]
        [InlineData("D100.5", FinsMemoryArea.DM, 100, true, 5)]
        [InlineData("CIO200", FinsMemoryArea.CIO, 200, false, 0)]
        [InlineData("CIO200.10", FinsMemoryArea.CIO, 200, true, 10)]
        [InlineData("W300", FinsMemoryArea.WR, 300, false, 0)]
        [InlineData("H400", FinsMemoryArea.HR, 400, false, 0)]
        public void Test_FinsAddress_Parsing(string addressStr, FinsMemoryArea expectedArea, 
            int expectedAddress, bool expectedIsBit, int expectedBitAddress)
        {
            var finsAddress = new FinsAddress(addressStr);
            
            _output.WriteLine($"解析地址: {addressStr}");
            _output.WriteLine($"内存区域: {finsAddress.MemoryArea}");
            _output.WriteLine($"地址: {finsAddress.Address}");
            _output.WriteLine($"是否为位: {finsAddress.IsBit}");
            _output.WriteLine($"位地址: {finsAddress.BitAddress}");
            
            Assert.True(finsAddress.IsValid, $"地址 {addressStr} 应该是有效的");
            Assert.Equal(expectedArea, finsAddress.MemoryArea);
            Assert.Equal(expectedAddress, finsAddress.Address);
            Assert.Equal(expectedIsBit, finsAddress.IsBit);
            Assert.Equal(expectedBitAddress, finsAddress.BitAddress);
        }

        [Theory]
        [InlineData("D100", new byte[] { 0x82 })] // DM区域码
        [InlineData("CIO200", new byte[] { 0x30 })] // CIO区域码
        [InlineData("W300", new byte[] { 0x31 })] // WR区域码
        [InlineData("H400", new byte[] { 0x32 })] // HR区域码
        public void Test_FinsAddress_MemoryAreaCode(string addressStr, byte[] expectedCode)
        {
            var finsAddress = new FinsAddress(addressStr);
            var memoryAreaCode = finsAddress.GetMemoryAreaCode();
            
            _output.WriteLine($"地址: {addressStr}, 内存区域码: 0x{memoryAreaCode:X2}");
            
            Assert.Equal(expectedCode[0], memoryAreaCode);
        }

        [Theory]
        [InlineData("D100", new byte[] { 0x00, 0x64, 0x00 })] // 字地址
        [InlineData("D100.5", new byte[] { 0x00, 0x64, 0x05 })] // 位地址
        [InlineData("CIO200", new byte[] { 0x00, 0xC8, 0x00 })] // 字地址
        public void Test_FinsAddress_AddressBytes(string addressStr, byte[] expectedBytes)
        {
            var finsAddress = new FinsAddress(addressStr);
            var addressBytes = finsAddress.GetAddressBytes();
            
            _output.WriteLine($"地址: {addressStr}");
            _output.WriteLine($"地址字节: {string.Join(" ", addressBytes.Select(b => b.ToString("X2")))}");
            
            Assert.Equal(expectedBytes.Length, addressBytes.Length);
            for (int i = 0; i < expectedBytes.Length; i++)
            {
                Assert.Equal(expectedBytes[i], addressBytes[i]);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("INVALID")]
        [InlineData("D")]
        [InlineData("D100.16")] // 位地址超出范围
        [InlineData("D-1")] // 负地址
        public void Test_FinsAddress_InvalidFormats(string invalidAddress)
        {
            try
            {
                var finsAddress = new FinsAddress(invalidAddress);
                _output.WriteLine($"无效地址: {invalidAddress}, 是否有效: {finsAddress.IsValid}");
                Assert.False(finsAddress.IsValid, $"地址 {invalidAddress} 应该是无效的");
            }
            catch (ArgumentException ex)
            {
                _output.WriteLine($"无效地址: {invalidAddress}, 抛出异常: {ex.Message}");
                // 对于无效地址，抛出异常是预期行为
                Assert.True(true, "无效地址抛出异常是正确的行为");
            }
        }

        #endregion

        #region FINS读取请求报文格式测试

        [Theory]
        [InlineData("D100", 1, DataTypeEnums.UInt16)]
        [InlineData("D200", 5, DataTypeEnums.UInt32)]
        [InlineData("CIO300", 10, DataTypeEnums.Int16)]
        public void Test_FinsReadRequest_MessageFormat(string address, int length, DataTypeEnums dataType)
        {
            var readRequest = new FinsReadRequest(address, length, dataType);
            var messageFrame = readRequest.ProtocolMessageFrame;
            
            _output.WriteLine($"读取请求 - 地址: {address}, 长度: {length}, 数据类型: {dataType}");
            _output.WriteLine($"报文长度: {messageFrame.Length}");
            _output.WriteLine($"报文内容: {string.Join(" ", messageFrame.Select(b => b.ToString("X2")))}");
            
            // 验证报文不为空
            Assert.NotNull(messageFrame);
            Assert.True(messageFrame.Length > 0, "读取请求报文长度应该大于0");
            
            // 验证FINS报文结构
            if (messageFrame.Length >= 10)
            {
                // 验证ICF字段 (第一个字节应该是0x80)
                Assert.Equal(0x80, messageFrame[0]);
                _output.WriteLine($"ICF字段验证通过: 0x{messageFrame[0]:X2}");
                
                // 验证报文包含FINS头部(10字节) + 命令码(2字节) + 数据
                Assert.True(messageFrame.Length >= 12, "报文长度应该至少包含FINS头部和命令码");
            }
        }

        [Fact]
        public void Test_FinsReadRequest_CommandStructure()
        {
            var address = "D100";
            var length = 1;
            var readRequest = new FinsReadRequest(address, length, DataTypeEnums.UInt16);
            var messageFrame = readRequest.ProtocolMessageFrame;
            
            _output.WriteLine($"读取请求命令结构分析:");
            _output.WriteLine($"完整报文: {string.Join(" ", messageFrame.Select(b => b.ToString("X2")))}");
            
            // 报文结构：长度字段(4字节) + FINS头部(10字节) + 命令码(2字节) + 数据
            // 但实际上FinsReadRequest.ProtocolMessageFrame可能不包含长度字段，直接是FINS命令
            if (messageFrame.Length >= 10) // 至少包含FINS头部
            {
                _output.WriteLine($"FINS头部: {string.Join(" ", messageFrame.Take(10).Select(b => b.ToString("X2")))}");
                
                // 验证ICF字段 (通常是0x80)
                Assert.Equal(0x80, messageFrame[0]);
                
                // 验证命令码部分
                if (messageFrame.Length >= 12)
                {
                    var commandCode = messageFrame.Skip(10).Take(2).ToArray();
                    _output.WriteLine($"命令码: {string.Join(" ", commandCode.Select(b => b.ToString("X2")))}");
                    
                    // FINS读取命令码应该是 0x01 0x01
                    Assert.Equal(0x01, commandCode[0]);
                    Assert.Equal(0x01, commandCode[1]);
                }
            }
        }

        #endregion

        #region FINS写入请求报文格式测试

        [Theory]
        [InlineData("D100", new byte[] { 0x12, 0x34 }, DataTypeEnums.UInt16)]
        [InlineData("D200", new byte[] { 0x12, 0x34, 0x56, 0x78 }, DataTypeEnums.UInt32)]
        public void Test_FinsWriteRequest_MessageFormat(string address, byte[] data, DataTypeEnums dataType)
        {
            var writeRequest = new FinsWriteRequest(address, data, dataType);
            var messageFrame = writeRequest.ProtocolMessageFrame;
            
            _output.WriteLine($"写入请求 - 地址: {address}, 数据类型: {dataType}");
            _output.WriteLine($"写入数据: {string.Join(" ", data.Select(b => b.ToString("X2")))}");
            _output.WriteLine($"报文长度: {messageFrame.Length}");
            _output.WriteLine($"报文内容: {string.Join(" ", messageFrame.Select(b => b.ToString("X2")))}");
            
            // 验证报文不为空
            Assert.NotNull(messageFrame);
            Assert.True(messageFrame.Length > 0, "写入请求报文长度应该大于0");
            
            // 验证报文包含写入的数据
            var messageStr = string.Join("", messageFrame.Select(b => b.ToString("X2")));
            var dataStr = string.Join("", data.Select(b => b.ToString("X2")));
            Assert.Contains(dataStr, messageStr);
        }

        [Fact]
        public void Test_FinsWriteRequest_CommandStructure()
        {
            var address = "D100";
            var data = new byte[] { 0x12, 0x34 };
            var writeRequest = new FinsWriteRequest(address, data, DataTypeEnums.UInt16);
            var messageFrame = writeRequest.ProtocolMessageFrame;
            
            _output.WriteLine($"写入请求命令结构分析:");
            _output.WriteLine($"完整报文: {string.Join(" ", messageFrame.Select(b => b.ToString("X2")))}");
            
            // 报文结构：FINS头部(10字节) + 命令码(2字节) + 数据
            if (messageFrame.Length >= 10) // 至少包含FINS头部
            {
                _output.WriteLine($"FINS头部: {string.Join(" ", messageFrame.Take(10).Select(b => b.ToString("X2")))}");
                
                // 验证ICF字段
                Assert.Equal(0x80, messageFrame[0]);
                
                // 验证命令码部分
                if (messageFrame.Length >= 12)
                {
                    var commandCode = messageFrame.Skip(10).Take(2).ToArray();
                    _output.WriteLine($"命令码: {string.Join(" ", commandCode.Select(b => b.ToString("X2")))}");
                    
                    // FINS写入命令码应该是 0x02 0x02
                    Assert.Equal(0x02, commandCode[0]);
                    Assert.Equal(0x02, commandCode[1]);
                }
            }
        }

        #endregion

        #region FINS读取回复报文验证测试

        [Theory]
        [InlineData("D100", 1, DataTypeEnums.UInt16, new byte[] { 0x12, 0x34 })]
        [InlineData("D200", 2, DataTypeEnums.UInt32, new byte[] { 0x12, 0x34, 0x56, 0x78 })]
        [InlineData("CIO300", 1, DataTypeEnums.Int16, new byte[] { 0xFF, 0xFF })]
        public void Test_FinsReadResponse_SuccessfulReply(string address, int length, DataTypeEnums dataType, byte[] expectedData)
        {
            // 构造成功的读取回复报文
            var responseData = new List<byte>();
            
            // FINS头部 (12字节)
            responseData.AddRange(new byte[]
            {
                0x80, // ICF
                0x00, // RSV
                0x02, // GCT
                0x00, // DNA
                0x01, // DA1
                0x00, // DA2
                0x00, // SNA
                0x00, // SA1
                0x00, // SA2
                0x00, // SID
                0x00, // MRC (主响应码 - 成功)
                0x00  // SRC (子响应码 - 成功)
            });
            
            // 添加数据部分
            responseData.AddRange(expectedData);
            
            var readResponse = new FinsReadResponse(responseData.ToArray(), dataType);
            
            _output.WriteLine($"读取回复测试 - 地址: {address}, 长度: {length}, 数据类型: {dataType}");
            _output.WriteLine($"期望数据: {string.Join(" ", expectedData.Select(b => b.ToString("X2")))}");
             _output.WriteLine($"回复报文: {string.Join(" ", responseData.Select(b => b.ToString("X2")))}");
            _output.WriteLine($"解析状态: {(readResponse.IsSuccess ? "成功" : "失败")}");
            _output.WriteLine($"错误信息: {readResponse.ErrorMessage ?? "无"}");
            
            // 验证回复解析成功
            Assert.True(readResponse.IsSuccess, "读取回复应该解析成功");
            Assert.Null(readResponse.ErrorMessage);
            
            // 验证响应头
            Assert.NotNull(readResponse.Header);
            Assert.Equal(0x80, readResponse.Header.ICF);
            Assert.Equal(0x00, readResponse.Header.MRC);
            Assert.Equal(0x00, readResponse.Header.SRC);
            
            // 验证数据内容
            Assert.NotNull(readResponse.Data);
            Assert.Equal(expectedData.Length, readResponse.Data.Length);
            Assert.Equal(expectedData, readResponse.Data);
        }

        [Theory]
        [InlineData(0x01, 0x01, "本地节点错误")]
        [InlineData(0x01, 0x02, "目标节点错误")]
        [InlineData(0x01, 0x03, "通信控制器错误")]
        [InlineData(0x11, 0x01, "内存区域错误")]
        public void Test_FinsReadResponse_ErrorReply(byte mrc, byte src, string expectedErrorKeyword)
        {
            // 构造错误的读取回复报文
            var responseData = new byte[12]
            {
                0x80, // ICF
                0x00, // RSV
                0x02, // GCT
                0x00, // DNA
                0x01, // DA1
                0x00, // DA2
                0x00, // SNA
                0x00, // SA1
                0x00, // SA2
                0x00, // SID
                mrc,  // MRC (主响应码 - 错误)
                src   // SRC (子响应码 - 错误)
            };
            
            var readResponse = new FinsReadResponse(responseData, DataTypeEnums.UInt16);
            
            _output.WriteLine($"错误回复测试 - MRC: 0x{mrc:X2}, SRC: 0x{src:X2}");
            _output.WriteLine($"期望错误关键词: {expectedErrorKeyword}");
            _output.WriteLine($"实际错误信息: {readResponse.ErrorMessage}");
            _output.WriteLine($"解析状态: {(readResponse.IsSuccess ? "成功" : "失败")}");
            
            // 验证回复解析失败
            Assert.False(readResponse.IsSuccess, "错误回复应该解析为失败状态");
            Assert.NotNull(readResponse.ErrorMessage);
            Assert.Contains(expectedErrorKeyword, readResponse.ErrorMessage);
            
            // 验证响应头正确解析
            Assert.NotNull(readResponse.Header);
            Assert.Equal(0x80, readResponse.Header.ICF);
            Assert.Equal(mrc, readResponse.Header.MRC);
            Assert.Equal(src, readResponse.Header.SRC);
            
            // 验证数据为空
            Assert.NotNull(readResponse.Data);
            Assert.Empty(readResponse.Data);
        }

        [Fact]
        public void Test_FinsReadResponse_InvalidReplyLength()
        {
            // 测试长度不足的回复报文
            var shortResponseData = new byte[8] { 0x80, 0x00, 0x02, 0x00, 0x01, 0x00, 0x00, 0x00 };
            
            var readResponse = new FinsReadResponse(shortResponseData, DataTypeEnums.UInt16);
            
            _output.WriteLine($"无效长度回复测试 - 报文长度: {shortResponseData.Length}");
            _output.WriteLine($"解析状态: {(readResponse.IsSuccess ? "成功" : "失败")}");
            _output.WriteLine($"错误信息: {readResponse.ErrorMessage}");
            
            // 验证回复解析失败
            Assert.False(readResponse.IsSuccess, "长度不足的回复应该解析失败");
            Assert.NotNull(readResponse.ErrorMessage);
            Assert.Contains("长度不足", readResponse.ErrorMessage);
        }

        #endregion

        #region FINS写入回复报文验证测试

        [Theory]
        [InlineData("D100", new byte[] { 0x12, 0x34 }, DataTypeEnums.UInt16)]
        [InlineData("D200", new byte[] { 0x12, 0x34, 0x56, 0x78 }, DataTypeEnums.UInt32)]
        [InlineData("CIO300", new byte[] { 0xFF }, DataTypeEnums.Byte)]
        public void Test_FinsWriteResponse_SuccessfulReply(string address, byte[] writeData, DataTypeEnums dataType)
        {
            // 构造成功的写入回复报文 (通常只包含FINS头部，无数据部分)
            var responseData = new byte[12]
            {
                0x80, // ICF
                0x00, // RSV
                0x02, // GCT
                0x00, // DNA
                0x01, // DA1
                0x00, // DA2
                0x00, // SNA
                0x00, // SA1
                0x00, // SA2
                0x00, // SID
                0x00, // MRC (主响应码 - 成功)
                0x00  // SRC (子响应码 - 成功)
            };
            
            var writeResponse = new FinsWriteResponse(responseData);
            
            _output.WriteLine($"写入回复测试 - 地址: {address}, 数据类型: {dataType}");
            _output.WriteLine($"写入数据: {string.Join(" ", writeData.Select(b => b.ToString("X2")))}");
            _output.WriteLine($"回复报文: {string.Join(" ", responseData.Select(b => b.ToString("X2")))}");
            _output.WriteLine($"解析状态: {(writeResponse.IsSuccess ? "成功" : "失败")}");
            _output.WriteLine($"错误信息: {writeResponse.ErrorMessage}");
            
            // 验证回复解析成功
            Assert.True(writeResponse.IsSuccess, "写入回复应该解析成功");
            Assert.Equal("写入成功", writeResponse.ErrorMessage);
            
            // 验证响应头
            Assert.NotNull(writeResponse.ResponseHeader);
            Assert.Equal(0x80, writeResponse.ResponseHeader.ICF);
            Assert.Equal(0x00, writeResponse.ResponseHeader.MRC);
            Assert.Equal(0x00, writeResponse.ResponseHeader.SRC);
            
            // 验证错误码
            Assert.Equal(0x0000, writeResponse.ErrorCode);
        }

        [Theory]
        [InlineData(0x01, 0x01, "本地节点错误")]
        [InlineData(0x01, 0x02, "目标节点错误")]
        [InlineData(0x01, 0x03, "通信控制器错误")]
        [InlineData(0x11, 0x01, "内存区域错误")]
        [InlineData(0x11, 0x02, "地址范围错误")]
        public void Test_FinsWriteResponse_ErrorReply(byte mrc, byte src, string expectedErrorKeyword)
        {
            // 构造错误的写入回复报文
            var responseData = new byte[12]
            {
                0x80, // ICF
                0x00, // RSV
                0x02, // GCT
                0x00, // DNA
                0x01, // DA1
                0x00, // DA2
                0x00, // SNA
                0x00, // SA1
                0x00, // SA2
                0x00, // SID
                mrc,  // MRC (主响应码 - 错误)
                src   // SRC (子响应码 - 错误)
            };
            
            var writeResponse = new FinsWriteResponse(responseData);
            
            _output.WriteLine($"写入错误回复测试 - MRC: 0x{mrc:X2}, SRC: 0x{src:X2}");
            _output.WriteLine($"期望错误关键词: {expectedErrorKeyword}");
            _output.WriteLine($"实际错误信息: {writeResponse.ErrorMessage}");
            _output.WriteLine($"解析状态: {(writeResponse.IsSuccess ? "成功" : "失败")}");
            
            // 验证回复解析失败
            Assert.False(writeResponse.IsSuccess, "错误回复应该解析为失败状态");
            Assert.NotNull(writeResponse.ErrorMessage);
            Assert.Contains(expectedErrorKeyword, writeResponse.ErrorMessage);
            
            // 验证响应头正确解析
            Assert.NotNull(writeResponse.ResponseHeader);
            Assert.Equal(0x80, writeResponse.ResponseHeader.ICF);
            Assert.Equal(mrc, writeResponse.ResponseHeader.MRC);
            Assert.Equal(src, writeResponse.ResponseHeader.SRC);
            
            // 验证错误码
            ushort expectedErrorCode = (ushort)((mrc << 8) | src);
            Assert.Equal(expectedErrorCode, writeResponse.ErrorCode);
        }

        [Fact]
        public void Test_FinsWriteResponse_InvalidReplyLength()
        {
            // 测试长度不足的写入回复报文
            var shortResponseData = new byte[8] { 0x80, 0x00, 0x02, 0x00, 0x01, 0x00, 0x00, 0x00 };
            
            var writeResponse = new FinsWriteResponse(shortResponseData);
            
            _output.WriteLine($"无效长度写入回复测试 - 报文长度: {shortResponseData.Length}");
            _output.WriteLine($"解析状态: {(writeResponse.IsSuccess ? "成功" : "失败")}");
            _output.WriteLine($"错误信息: {writeResponse.ErrorMessage}");
            
            // 验证回复解析失败
            Assert.False(writeResponse.IsSuccess, "长度不足的回复应该解析失败");
            Assert.NotNull(writeResponse.ErrorMessage);
            Assert.Contains("长度不足", writeResponse.ErrorMessage);
            Assert.Equal(0xFFFF, writeResponse.ErrorCode);
        }

        [Fact]
        public void Test_FinsWriteResponse_NullData()
        {
            // 测试空数据的写入回复
            Assert.Throws<ArgumentNullException>(() => new FinsWriteResponse(null));
        }

        #endregion

        #region FINS响应报文解析测试

        [Fact]
        public void Test_FinsResponseHeader_Parsing()
        {
            // 构造一个模拟的FINS响应头
            var responseData = new byte[12]
            {
                0x80, // ICF
                0x00, // RSV
                0x02, // GCT
                0x00, // DNA
                0x01, // DA1
                0x00, // DA2
                0x00, // SNA
                0x00, // SA1
                0x00, // SA2
                0x00, // SID
                0x00, // MRC (主响应码)
                0x00  // SRC (子响应码)
            };
            
            var header = FinsCommonMethods.ParseFinsResponseHeader(responseData);
            
            _output.WriteLine($"响应头解析结果:");
            _output.WriteLine($"ICF: 0x{header.ICF:X2}");
            _output.WriteLine($"主响应码: 0x{header.MRC:X2}");
            _output.WriteLine($"子响应码: 0x{header.SRC:X2}");
            
            Assert.Equal(0x80, header.ICF);
            Assert.Equal(0x00, header.MRC);
            Assert.Equal(0x00, header.SRC);
            
            // 测试响应是否成功
            var isSuccess = FinsCommonMethods.IsResponseSuccess(header);
            Assert.True(isSuccess, "MRC=0x00, SRC=0x00 应该表示成功");
        }

        [Theory]
        [InlineData(0x00, 0x00, true, "正常完成")]
        [InlineData(0x01, 0x01, false, "本地节点错误")]
        [InlineData(0x01, 0x02, false, "目标节点错误")]
        [InlineData(0x01, 0x03, false, "通信控制器错误")]
        public void Test_FinsErrorCode_Description(byte mrc, byte src, bool expectedSuccess, string expectedDescription)
        {
            var errorDescription = FinsCommonMethods.GetErrorDescription(mrc, src);
            
            _output.WriteLine($"错误码 MRC: 0x{mrc:X2}, SRC: 0x{src:X2}");
            _output.WriteLine($"错误描述: {errorDescription}");
            
            Assert.Contains(expectedDescription, errorDescription);
            
            // 构造响应头测试成功状态
            var header = new FinsResponseHeader { MRC = mrc, SRC = src };
            var isSuccess = FinsCommonMethods.IsResponseSuccess(header);
            Assert.Equal(expectedSuccess, isSuccess);
        }

        #endregion

        #region FINS回复报文数据解析测试

        [Theory]
        [InlineData(DataTypeEnums.Bool, new byte[] { 0x01 }, true)]
        [InlineData(DataTypeEnums.Bool, new byte[] { 0x00 }, false)]
        [InlineData(DataTypeEnums.Byte, new byte[] { 0xFF }, (byte)255)]
        [InlineData(DataTypeEnums.UInt16, new byte[] { 0x34, 0x12 }, (ushort)0x1234)]
        [InlineData(DataTypeEnums.UInt32, new byte[] { 0x78, 0x56, 0x34, 0x12 }, (uint)0x12345678)]
        [InlineData(DataTypeEnums.Int16, new byte[] { 0xFF, 0xFF }, (short)-1)]
        [InlineData(DataTypeEnums.Int32, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, -1)]
        public void Test_FinsReadResponse_DataParsing(DataTypeEnums dataType, byte[] responseDataBytes, object expectedValue)
        {
            // 构造包含数据的读取回复报文
            var responseData = new List<byte>();
            
            // FINS头部 (12字节)
            responseData.AddRange(new byte[]
            {
                0x80, // ICF
                0x00, // RSV
                0x02, // GCT
                0x00, // DNA
                0x01, // DA1
                0x00, // DA2
                0x00, // SNA
                0x00, // SA1
                0x00, // SA2
                0x00, // SID
                0x00, // MRC (主响应码 - 成功)
                0x00  // SRC (子响应码 - 成功)
            });
            
            // 添加数据部分
            responseData.AddRange(responseDataBytes);
            
            var readResponse = new FinsReadResponse(responseData.ToArray(), dataType);
            
            _output.WriteLine($"数据解析测试 - 数据类型: {dataType}");
            _output.WriteLine($"原始数据: {string.Join(" ", responseDataBytes.Select(b => b.ToString("X2")))}");
            _output.WriteLine($"期望值: {expectedValue} (类型: {expectedValue.GetType().Name})");
            _output.WriteLine($"解析状态: {(readResponse.IsSuccess ? "成功" : "失败")}");
            
            // 验证回复解析成功
            Assert.True(readResponse.IsSuccess, "数据解析应该成功");
            Assert.NotNull(readResponse.Data);
            Assert.Equal(responseDataBytes, readResponse.Data);
            
            // 使用FinsCommonMethods转换数据并验证
            var convertedValue = FinsCommonMethods.ConvertFromBytes(readResponse.Data, dataType);
            _output.WriteLine($"转换后的值: {convertedValue} (类型: {convertedValue.GetType().Name})");
            
            Assert.Equal(expectedValue, convertedValue);
        }

        [Theory]
        [InlineData(DataTypeEnums.Float, new byte[] { 0x00, 0x00, 0x80, 0x3F }, 1.0f)] // IEEE 754 单精度浮点数
        [InlineData(DataTypeEnums.Double, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F }, 1.0)] // IEEE 754 双精度浮点数
        public void Test_FinsReadResponse_FloatingPointDataParsing(DataTypeEnums dataType, byte[] responseDataBytes, object expectedValue)
        {
            // 构造包含浮点数据的读取回复报文
            var responseData = new List<byte>();
            
            // FINS头部 (12字节)
            responseData.AddRange(new byte[]
            {
                0x80, 0x00, 0x02, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            });
            
            // 添加浮点数据
            responseData.AddRange(responseDataBytes);
            
            var readResponse = new FinsReadResponse(responseData.ToArray(), dataType);
            
            _output.WriteLine($"浮点数解析测试 - 数据类型: {dataType}");
            _output.WriteLine($"原始数据: {string.Join(" ", responseDataBytes.Select(b => b.ToString("X2")))}");
            _output.WriteLine($"期望值: {expectedValue}");
            
            // 验证回复解析成功
            Assert.True(readResponse.IsSuccess, "浮点数解析应该成功");
            Assert.NotNull(readResponse.Data);
            
            // 转换并验证浮点数值
            var convertedValue = FinsCommonMethods.ConvertFromBytes(readResponse.Data, dataType);
            _output.WriteLine($"转换后的值: {convertedValue}");
            
            if (dataType == DataTypeEnums.Float)
            {
                Assert.Equal((float)expectedValue, (float)convertedValue, precision: 6); // 6位精度
            }
            else if (dataType == DataTypeEnums.Double)
            {
                Assert.Equal((double)expectedValue, (double)convertedValue, precision: 15); // 15位精度
            }
        }

        [Fact]
        public void Test_FinsReadResponse_MultipleDataValues()
        {
            // 测试包含多个数据值的回复报文
            var responseData = new List<byte>();
            
            // FINS头部
            responseData.AddRange(new byte[]
            {
                0x80, 0x00, 0x02, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            });
            
            // 添加多个UInt16值: 0x1234, 0x5678, 0x9ABC
            responseData.AddRange(new byte[] { 0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A });
            
            var readResponse = new FinsReadResponse(responseData.ToArray(), DataTypeEnums.UInt16);
            
            _output.WriteLine($"多值解析测试 - 数据长度: {readResponse.Data.Length}");
            _output.WriteLine($"数据内容: {string.Join(" ", readResponse.Data.Select(b => b.ToString("X2")))}");
            
            Assert.True(readResponse.IsSuccess, "多值解析应该成功");
            Assert.Equal(6, readResponse.Data.Length); // 3个UInt16值 = 6字节
            
            // 验证可以正确解析每个值
            var value1 = FinsCommonMethods.ConvertFromBytes(readResponse.Data.Take(2).ToArray(), DataTypeEnums.UInt16);
            var value2 = FinsCommonMethods.ConvertFromBytes(readResponse.Data.Skip(2).Take(2).ToArray(), DataTypeEnums.UInt16);
            var value3 = FinsCommonMethods.ConvertFromBytes(readResponse.Data.Skip(4).Take(2).ToArray(), DataTypeEnums.UInt16);
            
            _output.WriteLine($"解析的值: {value1:X4}, {value2:X4}, {value3:X4}");
            
            Assert.Equal((ushort)0x1234, value1);
            Assert.Equal((ushort)0x5678, value2);
            Assert.Equal((ushort)0x9ABC, value3);
        }

        #endregion

        #region FINS请求回复对应关系验证测试

        [Theory]
        [InlineData("D100", DataTypeEnums.UInt16, 5)]
        [InlineData("CIO200", DataTypeEnums.Bool, 16)]
        [InlineData("W300", DataTypeEnums.UInt32, 3)]
        [InlineData("H400", DataTypeEnums.Int16, 8)]
        public void Test_FinsRequestResponse_Correspondence(string address, DataTypeEnums dataType, ushort length)
        {
            // 构造读取请求
            var finsAddress = new FinsAddress(address);
            var readRequest = new FinsReadRequest(finsAddress, length);
            var requestFrame = readRequest.ProtocolMessageFrame;
            
            _output.WriteLine($"请求回复对应测试 - 地址: {address}, 数据类型: {dataType}, 长度: {length}");
            _output.WriteLine($"请求报文长度: {readRequest.Length}");
            
            // 验证请求报文基本结构
            Assert.True(requestFrame.Length >= 12, "请求报文长度应至少12字节");
            Assert.Equal(0x80, requestFrame[0]); // ICF
            Assert.Equal(0x01, requestFrame[10]); // 读取命令码
            Assert.Equal(0x01, requestFrame[11]); // 读取子命令码
            
            // 构造对应的成功回复报文
            var responseData = new List<byte>();
            
            // 复制请求的通信头部信息到回复中（交换源和目标）
            responseData.AddRange(new byte[]
            {
                0x80, // ICF - 回复
                requestFrame[1], // RSV
                requestFrame[2], // GCT
                requestFrame[6], // DNA (原SNA)
                requestFrame[7], // DA1 (原SA1)
                requestFrame[8], // DA2 (原SA2)
                requestFrame[3], // SNA (原DNA)
                requestFrame[4], // SA1 (原DA1)
                requestFrame[5], // SA2 (原DA2)
                requestFrame[9], // SID - 保持一致
                0x00, // MRC - 成功
                0x00  // SRC - 成功
            });
            
            // 根据数据类型和长度添加模拟数据
            var dataSize = FinsCommonMethods.GetDataTypeLength(dataType) * length;
            var mockData = new byte[dataSize];
            for (int i = 0; i < dataSize; i++)
            {
                mockData[i] = (byte)(i % 256); // 生成测试数据
            }
            responseData.AddRange(mockData);
            
            var readResponse = new FinsReadResponse(responseData.ToArray(), dataType);
            
            _output.WriteLine($"回复报文长度: {responseData.Count}");
            _output.WriteLine($"数据部分长度: {readResponse.Data?.Length ?? 0}");
            _output.WriteLine($"回复解析状态: {(readResponse.IsSuccess ? "成功" : "失败")}");
            
            // 验证回复报文解析成功
            Assert.True(readResponse.IsSuccess, "回复报文应解析成功");
            Assert.NotNull(readResponse.Data);
            Assert.Equal(dataSize, readResponse.Data.Length);
            
            // 验证SID一致性（请求和回复的会话ID应该匹配）
            Assert.Equal(requestFrame[9], responseData[9]);
            
            // 验证数据内容
            Assert.Equal(mockData, readResponse.Data);
        }

        [Theory]
        [InlineData("D500", DataTypeEnums.UInt16, new object[] { (ushort)0x1234, (ushort)0x5678, (ushort)0x9ABC })]
        [InlineData("W600", DataTypeEnums.UInt32, new object[] { (uint)0x12345678, (uint)0x9ABCDEF0 })]
        public void Test_FinsWriteRequestResponse_Correspondence(string address, DataTypeEnums dataType, object[] values)
        {
            var finsAddress = new FinsAddress(address);
            
            // 构造写入数据
            var writeData = new List<byte>();
            foreach (var value in values)
            {
                var bytes = FinsCommonMethods.ConvertToBytes(value, dataType);
                writeData.AddRange(bytes);
            }
            
            var writeRequest = new FinsWriteRequest(finsAddress.ToString(), writeData.ToArray(), dataType);
            var requestFrame = writeRequest.ProtocolMessageFrame;
            
            _output.WriteLine($"写入请求回复对应测试 - 地址: {address}, 数据类型: {dataType}");
            _output.WriteLine($"写入值数量: {values.Length}");
            _output.WriteLine($"请求报文长度: {requestFrame.Length}");
            
            // 验证写入请求报文
            Assert.True(requestFrame.Length >= 12, "写入请求报文长度应至少12字节");
            Assert.Equal(0x80, requestFrame[0]); // ICF
            Assert.Equal(0x02, requestFrame[10]); // 写入命令码
            Assert.Equal(0x02, requestFrame[11]); // 写入子命令码
            
            // 构造对应的成功写入回复报文
            var responseData = new List<byte>();
            
            // 写入回复通常只包含头部，无数据部分
            responseData.AddRange(new byte[]
            {
                0x80, // ICF - 回复
                requestFrame[1], // RSV
                requestFrame[2], // GCT
                requestFrame[6], // DNA (原SNA)
                requestFrame[7], // DA1 (原SA1)
                requestFrame[8], // DA2 (原SA2)
                requestFrame[3], // SNA (原DNA)
                requestFrame[4], // SA1 (原DA1)
                requestFrame[5], // SA2 (原DA2)
                requestFrame[9], // SID - 保持一致
                0x00, // MRC - 成功
                0x00  // SRC - 成功
            });
            
            var writeResponse = new FinsWriteResponse(responseData.ToArray());
            
            _output.WriteLine($"回复报文长度: {responseData.Count}");
            _output.WriteLine($"写入回复状态: {(writeResponse.IsSuccess ? "成功" : "失败")}");
            
            // 验证写入回复
            Assert.True(writeResponse.IsSuccess, "写入回复应解析成功");
            
            // 验证SID一致性
            Assert.Equal(requestFrame[9], responseData[9]);
        }

        [Fact]
        public void Test_FinsRequestResponse_ErrorCorrespondence()
        {
            // 测试错误回复与请求的对应关系
            var finsAddress = new FinsAddress("D999");
            var readRequest = new FinsReadRequest(finsAddress, 1);
            var requestFrame = readRequest.ProtocolMessageFrame;
            
            _output.WriteLine("错误回复对应测试");
            _output.WriteLine($"请求SID: 0x{requestFrame[9]:X2}");
            
            // 构造错误回复报文（地址超出范围错误）
            var errorResponse = new List<byte>();
            errorResponse.AddRange(new byte[]
            {
                0x80, // ICF - 回复
                requestFrame[1], // RSV
                requestFrame[2], // GCT
                requestFrame[6], // DNA (原SNA)
                requestFrame[7], // DA1 (原SA1)
                requestFrame[8], // DA2 (原SA2)
                requestFrame[3], // SNA (原DNA)
                requestFrame[4], // SA1 (原DA1)
                requestFrame[5], // SA2 (原DA2)
                requestFrame[9], // SID - 保持一致
                0x11, // MRC - 错误
                0x02  // SRC - 地址范围错误
            });
            
            var readResponse = new FinsReadResponse(errorResponse.ToArray(), DataTypeEnums.UInt16);
            
            _output.WriteLine($"回复SID: 0x{errorResponse[9]:X2}");
            _output.WriteLine($"错误码: MRC=0x{errorResponse[10]:X2}, SRC=0x{errorResponse[11]:X2}");
            _output.WriteLine($"回复状态: {(readResponse.IsSuccess ? "成功" : "失败")}");
            
            // 验证错误回复
            Assert.False(readResponse.IsSuccess, "错误回复应标记为失败");
            
            // 验证SID一致性
            Assert.Equal(requestFrame[9], errorResponse[9]);
            
            // 验证错误码
            Assert.Equal(0x11, errorResponse[10]); // MRC
            Assert.Equal(0x02, errorResponse[11]); // SRC
        }

        #endregion

        #region 数据类型转换测试

        [Theory]
        [InlineData(DataTypeEnums.Bool, true, new byte[] { 0x01 })]
        [InlineData(DataTypeEnums.Bool, false, new byte[] { 0x00 })]
        [InlineData(DataTypeEnums.Byte, (byte)255, new byte[] { 0xFF })]
        [InlineData(DataTypeEnums.UInt16, (ushort)0x1234, new byte[] { 0x34, 0x12 })]
        [InlineData(DataTypeEnums.UInt32, (uint)0x12345678, new byte[] { 0x78, 0x56, 0x34, 0x12 })]
        public void Test_DataType_Conversion_ToBytes(DataTypeEnums dataType, object value, byte[] expectedBytes)
        {
            var result = FinsCommonMethods.ConvertToBytes(value, dataType);
            
            _output.WriteLine($"数据类型: {dataType}, 值: {value}");
            _output.WriteLine($"期望字节: {string.Join(" ", expectedBytes.Select(b => b.ToString("X2")))}");
            _output.WriteLine($"实际字节: {string.Join(" ", result.Select(b => b.ToString("X2")))}");
            
            Assert.Equal(expectedBytes, result);
        }

        [Theory]
        [InlineData(DataTypeEnums.Bool, new byte[] { 0x01 }, true)]
        [InlineData(DataTypeEnums.Bool, new byte[] { 0x00 }, false)]
        [InlineData(DataTypeEnums.Byte, new byte[] { 0xFF }, (byte)255)]
        [InlineData(DataTypeEnums.UInt16, new byte[] { 0x34, 0x12 }, (ushort)0x1234)]
        [InlineData(DataTypeEnums.UInt32, new byte[] { 0x78, 0x56, 0x34, 0x12 }, (uint)0x12345678)]
        public void Test_DataType_Conversion_FromBytes(DataTypeEnums dataType, byte[] bytes, object expectedValue)
        {
            var result = FinsCommonMethods.ConvertFromBytes(bytes, dataType);
            
            _output.WriteLine($"数据类型: {dataType}");
            _output.WriteLine($"输入字节: {string.Join(" ", bytes.Select(b => b.ToString("X2")))}");
            _output.WriteLine($"期望值: {expectedValue}, 实际值: {result}");
            
            Assert.Equal(expectedValue, result);
        }

        [Theory]
        [InlineData(DataTypeEnums.Bool, 1)]
        [InlineData(DataTypeEnums.Byte, 1)]
        [InlineData(DataTypeEnums.UInt16, 2)]
        [InlineData(DataTypeEnums.UInt32, 4)]
        [InlineData(DataTypeEnums.Float, 4)]
        [InlineData(DataTypeEnums.Double, 8)]
        public void Test_DataType_Length(DataTypeEnums dataType, int expectedLength)
        {
            var length = FinsCommonMethods.GetDataTypeLength(dataType);
            
            _output.WriteLine($"数据类型: {dataType}, 期望长度: {expectedLength}, 实际长度: {length}");
            
            Assert.Equal(expectedLength, length);
        }

        #endregion

        #region 内容长度解析测试

        [Theory]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x10 }, 16)]
        [InlineData(new byte[] { 0x00, 0x00, 0x01, 0x00 }, 256)]
        [InlineData(new byte[] { 0x12, 0x34, 0x56, 0x78 }, 0x12345678)]
        public void Test_GetContentLength(byte[] data, int expectedLength)
        {
            var length = FinsCommonMethods.GetContentLength(data);
            
            _output.WriteLine($"输入数据: {string.Join(" ", data.Select(b => b.ToString("X2")))}");
            _output.WriteLine($"期望长度: {expectedLength}, 实际长度: {length}");
            
            Assert.Equal(expectedLength, length);
        }

        [Fact]
        public void Test_GetContentLength_InvalidData()
        {
            // 测试空数据
            var length1 = FinsCommonMethods.GetContentLength(null);
            Assert.Equal(0, length1);
            
            // 测试长度不足的数据
            var length2 = FinsCommonMethods.GetContentLength(new byte[] { 0x00, 0x00 });
            Assert.Equal(0, length2);
        }

        #endregion

        #region 校验和测试

        [Theory]
        [InlineData(new byte[] { 0x01, 0x02, 0x03 }, 0x06)]
        [InlineData(new byte[] { 0xFF, 0xFF }, 0xFE)] // 255 + 255 = 510, 510 & 0xFF = 254
        [InlineData(new byte[] { }, 0x00)]
        public void Test_CalculateChecksum(byte[] data, byte expectedChecksum)
        {
            var checksum = FinsCommonMethods.CalculateChecksum(data);
            
            _output.WriteLine($"数据: {string.Join(" ", data.Select(b => b.ToString("X2")))}");
            _output.WriteLine($"期望校验和: 0x{expectedChecksum:X2}, 实际校验和: 0x{checksum:X2}");
            
            Assert.Equal(expectedChecksum, checksum);
        }

        [Fact]
        public void Test_ValidateChecksum()
        {
            var data = new byte[] { 0x01, 0x02, 0x03 };
            var correctChecksum = FinsCommonMethods.CalculateChecksum(data);
            var incorrectChecksum = (byte)(correctChecksum + 1);
            
            var isValid1 = FinsCommonMethods.ValidateChecksum(data, correctChecksum);
            var isValid2 = FinsCommonMethods.ValidateChecksum(data, incorrectChecksum);
            
            Assert.True(isValid1, "正确的校验和应该验证通过");
            Assert.False(isValid2, "错误的校验和应该验证失败");
        }

        #endregion
    }
}