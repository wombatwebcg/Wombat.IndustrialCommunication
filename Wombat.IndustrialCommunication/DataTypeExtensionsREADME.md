# Wombat.Extensions.DataTypeExtensions

## 简介

Wombat.Extensions.DataTypeExtensions 是一个功能丰富的 .NET 数据类型转换扩展库，提供了多种数据类型之间的便捷转换方法。该库支持字符串、字节数组、数值类型等之间的互相转换，并提供了各种编码格式和端序处理功能。

## 功能特点

- **全面的数据类型转换**：支持字符串、字节数组、整型、浮点型等多种数据类型之间的相互转换
- **字节序处理**：支持大端序(ABCD)、小端序(DCBA)和中端序(BADC/CDAB)之间的数据转换
- **编码支持**：支持各种文本编码格式，如ASCII、UTF-8等
- **加密与哈希**：内置MD5、SHA1、SHA256等哈希算法
- **Base64处理**：提供标准Base64和URL安全Base64的编解码功能
- **二进制操作**：支持二进制数据的位操作和转换
- **高性能实现**：优化的内存使用和性能表现

## 环境要求

- .NET Standard 2.0 或更高版本

## 安装方法

通过NuGet包管理器安装：

```
PM> Install-Package Wombat.Extensions.DataTypeExtensions
```

或通过.NET CLI安装：

```
dotnet add package Wombat.Extensions.DataTypeExtensions
```

## 基本用法

首先引入命名空间：

```csharp
using Wombat.Extensions.DataTypeExtensions;
```

## 使用示例

### 字符串转换

```csharp
// 字符串转换为整数
string numStr = "123";
int num = numStr.ToInt();

// 字符串转换为十六进制字节数组
string hexStr = "1A2B3C";
byte[] bytes = hexStr.HexStringToBytes(false);

// 字符串Base64编码
string text = "Hello World";
string base64 = text.Base64Encode();
```

### 字节数组操作

```csharp
// 字节数组转换为十六进制字符串
byte[] data = new byte[] { 0x1A, 0x2B, 0x3C };
string hex = data.ToHexString();  // "1a2b3c"

// 字节数组转换为整数（支持不同字节序）
byte[] intBytes = new byte[] { 0x12, 0x34, 0x56, 0x78 };
int value = intBytes.ToInt32(0, EndianFormat.ABCD);  // 大端序
int value2 = intBytes.ToInt32(0, EndianFormat.DCBA); // 小端序

// 字节数组转换为字符串
byte[] textBytes = new byte[] { 72, 101, 108, 108, 111 };
string text = textBytes.ToString(Encoding.ASCII);  // "Hello"
```

### 数值类型转换

```csharp
// 整数转换为字节数组（支持不同字节序）
int number = 0x12345678;
byte[] bytes = number.ToByte(EndianFormat.ABCD);  // 大端序
byte[] bytes2 = number.ToByte(EndianFormat.DCBA); // 小端序

// 浮点数转换
double doubleValue = 123.45;
byte[] doubleBytes = doubleValue.ToByte();
```

### 加密和哈希

```csharp
// MD5哈希
string text = "test";
string md5 = text.ToMD5String();

// SHA1哈希
string sha1 = text.ToSHA1String();

// SHA256哈希
string sha256 = text.ToSHA256String();
```

### 二进制操作

```csharp
// 整数转二进制字符串
int num = 42;
string binary = num.IntToBinaryArray(8);  // "00101010"

// 二进制字符串转整数
string binaryStr = "00101010";
int number = binaryStr.BinaryArrayToInt();
```

## 高级特性

### 端序处理

该库支持不同的字节序格式：

- `EndianFormat.ABCD`：大端序 (Big-Endian)
- `EndianFormat.DCBA`：小端序 (Little-Endian)
- `EndianFormat.BADC`：中端序 (Big-endian byte swap)
- `EndianFormat.CDAB`：中端序 (Little-endian byte swap)

示例：

```csharp
// 转换32位整数，处理不同字节序
byte[] data = new byte[] { 0x12, 0x34, 0x56, 0x78 };

int valueABCD = data.ToInt32(0, EndianFormat.ABCD);  // 0x12345678
int valueDCBA = data.ToInt32(0, EndianFormat.DCBA);  // 0x78563412
int valueBADC = data.ToInt32(0, EndianFormat.BADC);  // 0x34127856
int valueCDAB = data.ToInt32(0, EndianFormat.CDAB);  // 0x56781234
```

## 贡献

欢迎提交问题报告和改进建议。如需贡献代码，请先与项目维护者讨论您的改动意图。

## 许可证

本项目采用MIT许可证。详情请参阅[LICENSE.md](LICENSE.md)文件。

## 联系方式

- GitHub仓库：https://github.com/wombatwebcg/Wombat.Extensions.DataTypeExtensions