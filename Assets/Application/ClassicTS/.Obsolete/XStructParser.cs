using GrokParser;
using System;
using System.IO;
using System.Text;

public class XStructParser
{
	public static XStruct LoadBinary(byte[] data)
	{
		if (data.Length < 4)
			throw new InvalidDataException("Data too short to contain version number.");

		// Read version
		float version = BitConverter.ToSingle(data, 0);
		if (version != 4.0f)
			throw new InvalidDataException($"Unsupported version: {version}");

		// Parse the stream starting after the version
		var xstruct = new XStruct();
		int offset = 4;
		InitFromStream(xstruct, data, ref offset);
		return xstruct;
	}

	private static void InitFromStream(XStruct xstruct, byte[] data, ref int offset)
	{
		// Read number of members
		if (offset + 4 > data.Length)
			throw new InvalidDataException("Unexpected end of data while reading member count.");
		int memberCount = BitConverter.ToInt32(data, offset);
		offset += 4;

		if (memberCount == 0) return;

		// Add members
		for (int i = 0; i < memberCount; i++)
		{
			// Read type
			string type = ReadNullTerminatedString(data, ref offset);
			if (type == null)
				throw new InvalidDataException("Failed to read type string.");

			// Read name
			string name = ReadNullTerminatedString(data, ref offset);
			if (name == null)
				throw new InvalidDataException("Failed to read name string.");

			var member = new XStruct { Type = type, Name = name };
			xstruct.Members.Add(member);

			// Handle data based on type
			if (type == "struct" || type == "array" || type == "data")
			{
				// Recursively parse nested structs
				InitFromStream(member, data, ref offset);
			}
			else if (type == "char")
			{
				// Read compression parameters from sibling members
				int nCompression = xstruct.Find("nCompression")?.GetData<int>() ?? 0x20;
				int nBits = nCompression & 0xF8;
				int nCompressedLength = xstruct.Find("nCompressedLength")?.GetData<int>() ?? 0;
				nCompressedLength *= (nBits >> 3);

				if (nCompressedLength < 0 || offset + nCompressedLength > data.Length)
					throw new InvalidDataException("Invalid or insufficient data for char array.");

				member.Data = new byte[nCompressedLength];
				Array.Copy(data, offset, member.Data, 0, nCompressedLength);
				offset += nCompressedLength;
			}
			else if (type == "string")
			{
				// Read null-terminated string data
				int start = offset;
				while (offset < data.Length && data[offset] != 0)
					offset++;
				if (offset >= data.Length)
					throw new InvalidDataException("Unterminated string data.");

				int length = offset - start + 1; // Include null terminator
				member.Data = new byte[length];
				Array.Copy(data, start, member.Data, 0, length);
				offset++; // Skip null terminator
			}
			else
			{
				// Handle fixed-size primitives
				int size = GetDataSize(type);
				if (size == 0)
					throw new InvalidDataException($"Unknown type: {type}");

				if (offset + size > data.Length)
					throw new InvalidDataException($"Not enough data for type {type}.");

				member.Data = new byte[size];
				Array.Copy(data, offset, member.Data, 0, size);
				offset += size;
			}
		}
	}

	private static string ReadNullTerminatedString(byte[] data, ref int offset)
	{
		int start = offset;
		while (offset < data.Length && data[offset] != 0)
			offset++;
		if (offset >= data.Length)
			return null;
		string result = Encoding.UTF8.GetString(data, start, offset - start);
		offset++; // Skip null terminator
		return result;
	}

	private static int GetDataSize(string type)
	{
		switch (type.ToLower())
		{
			case "bool":
				return sizeof(bool); // 1 byte
			case "float":
				return sizeof(float); // 4 bytes
			case "int":
				return sizeof(int); // 4 bytes
			case "string":
			case "char":
				return 0; // Size determined dynamically
			default:
				return 0; // Unknown type
		}
	}
}