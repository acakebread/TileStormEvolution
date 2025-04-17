using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MassiveHadron
{
	public class XStruct
	{
		private int m_nSize;
		private string m_pType;
		private string m_pName;
		private byte[] m_pData;
		private List<XStruct> m_pStruct;

		public XStruct()
		{
			m_nSize = 0;
			m_pType = null;
			m_pName = null;
			m_pData = null;
			m_pStruct = new List<XStruct>();
		}

		public XStruct(byte[] buffer)
		{
			m_nSize = 0;
			m_pType = null;
			m_pName = null;
			m_pData = null;
			m_pStruct = new List<XStruct>();
			InitFromStream(buffer);
		}

		public void Release()
		{
			m_pType = null;
			m_pData = null;
			m_pName = null;
			m_pStruct.Clear();
			m_nSize = 0;
		}

		public void SetType(string type) => m_pType = type;
		public void SetName(string name) => m_pName = name;
		public void SetData(byte[] data) => m_pData = data != null ? (byte[])data.Clone() : null;

		public XStruct FindRecursive(string name)
		{
			if (m_pName != null && name != null && string.Equals(m_pName, name, StringComparison.OrdinalIgnoreCase))
				return this;

			foreach (var structItem in m_pStruct)
			{
				XStruct result = structItem.FindRecursive(name);
				if (result != null) return result;
			}
			return null;
		}

		public XStruct Find(string name) =>
			m_pStruct.Find(s => string.Equals(s.m_pName, name, StringComparison.OrdinalIgnoreCase));

		public string GetString(string name)
		{
			XStruct member = Find(name);
			if (member != null && string.Equals(member.m_pType, "string", StringComparison.OrdinalIgnoreCase))
				return Encoding.UTF8.GetString(member.m_pData);
			return null;
		}

		public bool GetBool(string name)
		{
			XStruct member = Find(name);
			if (member != null && string.Equals(member.m_pType, "bool", StringComparison.OrdinalIgnoreCase))
				return BitConverter.ToBoolean(member.m_pData, 0);
			return false;
		}

		public float GetFloat(string name)
		{
			XStruct member = Find(name);
			if (member != null && string.Equals(member.m_pType, "float", StringComparison.OrdinalIgnoreCase))
				return BitConverter.ToSingle(member.m_pData, 0);
			return 0.0f;
		}

		public int GetInt(string name)
		{
			XStruct member = Find(name);
			if (member != null && string.Equals(member.m_pType, "int", StringComparison.OrdinalIgnoreCase))
				return BitConverter.ToInt32(member.m_pData, 0);
			return 0;
		}

		public byte[] GetBytes(string name)
		{
			XStruct member = Find(name);
			if (member != null && string.Equals(member.m_pType, "char", StringComparison.OrdinalIgnoreCase))
				return member.m_pData;
			return null;
		}

		public XStruct this[string name]
		{
			get
			{
				XStruct result = Find(name);
				if (result != null) return result;
				throw new KeyNotFoundException($"ERROR! XStruct[\"{name}\"] not found.");
			}
		}

		public XStruct Struct(int index)
		{
			int count = 0;
			foreach (var item in m_pStruct)
			{
				if (string.Equals(item.m_pType, "struct", StringComparison.OrdinalIgnoreCase))
				{
					if (index == 0) return item;
					index--;
				}
				count++;
			}
			throw new IndexOutOfRangeException($"ERROR! XStruct[{index}] not found.");
		}

		public int this[int index]
		{
			get
			{
				if (!string.Equals(m_pType, "array", StringComparison.OrdinalIgnoreCase))
					return 0;

				if (m_pData == null)
				{
					XStruct pData = Find("nTileIndex");
					if (pData == null) throw new InvalidOperationException("badly formed array");

					int nCompression = pData.GetInt("nCompression");
					if (nCompression == 0) nCompression = 0x20;
					int nBits = nCompression & 0xf8;
					byte[] pBytes = pData.GetBytes("bytes");
					int nSize = pData.GetInt("nUncompressedLength");

					m_pData = new byte[nSize * sizeof(int)];
					int nAdjust = pData.Find("nAdjust")?.GetInt("nAdjust") ?? 0;
					int nPos = 0;
					int bytePos = 0;

					while (nPos < nSize)
					{
						int nVal = GetInteger(pBytes, ref bytePos, nBits);
						int nRun = 1;
						if ((nCompression & 1) != 0 && nVal < 0)
						{
							nRun = -nVal;
							nVal = GetInteger(pBytes, ref bytePos, nBits);
						}
						while (nRun-- > 0 && nPos < nSize)
						{
							BitConverter.GetBytes(nVal + nAdjust).CopyTo(m_pData, nPos * sizeof(int));
							nPos++;
						}
					}
				}
				return BitConverter.ToInt32(m_pData, index * sizeof(int));
			}
		}

		public int InitFromStream(byte[] buffer)
		{
			int pos = 0;

			// Read size (4 bytes)
			if (pos + 4 > buffer.Length)
				throw new InvalidDataException("Buffer too short for size");
			m_nSize = BitConverter.ToInt32(buffer, pos);
			pos += sizeof(int);

			if (m_nSize > 0)
			{
				m_pStruct = new List<XStruct>(m_nSize);

				for (int i = 0; i < m_nSize; i++)
				{
					// Read type string
					int typeStart = pos;
					while (pos < buffer.Length && buffer[pos] != 0) pos++;
					if (pos >= buffer.Length) throw new InvalidDataException("Unexpected end of buffer in type string");
					string type = Encoding.UTF8.GetString(buffer, typeStart, pos - typeStart);
					pos++; // Skip null terminator

					// Read name string
					int nameStart = pos;
					while (pos < buffer.Length && buffer[pos] != 0) pos++;
					if (pos >= buffer.Length) throw new InvalidDataException("Unexpected end of buffer in name string");
					string name = Encoding.UTF8.GetString(buffer, nameStart, pos - nameStart);
					pos++; // Skip null terminator

					var newStruct = new XStruct();
					newStruct.SetType(type);
					newStruct.SetName(name);

					if (type.Equals("struct", StringComparison.OrdinalIgnoreCase) ||
						type.Equals("array", StringComparison.OrdinalIgnoreCase) ||
						type.Equals("data", StringComparison.OrdinalIgnoreCase))
					{
						int remainingBytes = buffer.Length - pos;
						if (remainingBytes <= 0) throw new InvalidDataException("No data left for nested struct");
						byte[] subBuffer = new byte[remainingBytes];
						Array.Copy(buffer, pos, subBuffer, 0, remainingBytes);
						int consumed = newStruct.InitFromStream(subBuffer);
						pos += consumed; // Advance position by bytes consumed
					}
					else if (type.Equals("char", StringComparison.OrdinalIgnoreCase))
					{
						int nCompression = newStruct.GetInt("nCompression");
						if (nCompression == 0) nCompression = 0x20;
						int nBits = nCompression & 0xf8;
						int nCompressedLength = newStruct.GetInt("nCompressedLength") * (nBits >> 3);
						if (pos + nCompressedLength > buffer.Length)
							throw new InvalidDataException("Buffer too short for char data");
						newStruct.SetData(buffer.Skip(pos).Take(nCompressedLength).ToArray());
						pos += nCompressedLength; // Advance position
					}
					else if (type.Equals("string", StringComparison.OrdinalIgnoreCase))
					{
						// Read string data until null terminator
						int dataStart = pos;
						while (pos < buffer.Length && buffer[pos] != 0) pos++;
						if (pos >= buffer.Length) throw new InvalidDataException("Unexpected end of buffer in string data");
						int dataLength = pos - dataStart + 1; // Include null terminator
						newStruct.SetData(buffer.Skip(dataStart).Take(dataLength).ToArray());
						pos++; // Skip null terminator
					}
					else
					{
						int dataSize = GetDataSize(type);
						if (dataSize == 0 && !type.Equals("string", StringComparison.OrdinalIgnoreCase))
							throw new InvalidDataException($"Unknown type '{type}' with zero size");
						if (pos + dataSize > buffer.Length)
							throw new InvalidDataException($"Buffer too short for {type} data");
						newStruct.SetData(buffer.Skip(pos).Take(dataSize).ToArray());
						pos += dataSize; // Advance position
					}
					m_pStruct.Add(newStruct);
				}
			}
			return pos; // Return total bytes consumed
		}

		private int GetDataSize(string type)
		{
			switch (type.ToLower())
			{
				case "bool": return sizeof(bool);
				case "float": return sizeof(float);
				case "int": return sizeof(int);
				case "string": return 0; // Variable length, handled separately
				case "char": return 0;   // Variable length, handled separately
				default: return 0;       // Unknown types will trigger an error
			}
		}

		public XStruct AddStruct()
		{
			var newStruct = new XStruct();
			m_pStruct.Add(newStruct);
			m_nSize++;
			return newStruct;
		}

		public XStruct AddMember(string type, string name, byte[] data = null)
		{
			var newStruct = AddStruct();
			if (newStruct != null)
			{
				newStruct.SetType(type);
				newStruct.SetName(name);
				if (data != null) newStruct.SetData(data);
			}
			return newStruct;
		}

		public XStruct AddString(string name, string value)
		{
			return AddMember("string", name, Encoding.UTF8.GetBytes(value + "\0"));
		}

		public XStruct AddBool(string name, bool value)
		{
			return AddMember("bool", name, BitConverter.GetBytes(value));
		}

		public XStruct AddFloat(string name, float value)
		{
			return AddMember("float", name, BitConverter.GetBytes(value));
		}

		public XStruct AddInt(string name, int value)
		{
			return AddMember("int", name, BitConverter.GetBytes(value));
		}

		public XStruct AddBytes(string name, byte[] bytes)
		{
			return AddMember("char", name, bytes);
		}

		public XStruct AddArray(string name, int[] data, int nX, int nZ)
		{
			XStruct pXStruct = AddMember("array", name);
			if (pXStruct != null)
			{
				pXStruct.AddInt("nWidth", nX);
				pXStruct.AddInt("nHeight", nZ);
				XStruct pArray = pXStruct.AddMember("data", "nTileIndex");

				if (pArray != null)
				{
					int nAdjust = 0;
					int nMax = int.MinValue;
					int nSize = nX * nZ;

					for (int i = 0; i < nSize; i++)
					{
						nMax = Math.Max(nMax, data[i]);
						nAdjust = Math.Min(nAdjust, data[i]);
					}
					nMax -= nAdjust;

					int nCompression = 0x20; // Default: raw int
					if (nMax < 128) nCompression = 0x09;        // RLE byte
					else if (nMax < 32768) nCompression = 0x11; // RLE word
					else nCompression = 0x21;                   // RLE int

					int nBits = nCompression & 0xf8;
					List<int> buffer = new List<int>();

					int pos = 0;
					while (pos < nSize)
					{
						int nRun = -1;
						int nVal = data[pos];
						pos++;
						if ((nCompression & 1) != 0)
						{
							while (pos < nSize && data[pos] == nVal && nRun > int.MinValue + 1)
							{
								pos++;
								nRun--;
							}
						}
						if (nRun < -1) buffer.Add(nRun);
						buffer.Add(nVal - nAdjust);
					}

					int nCompressedLength = buffer.Count * (nBits >> 3);
					byte[] pStream = new byte[nCompressedLength];
					int writePos = 0;
					foreach (int val in buffer)
					{
						writePos = WriteIntegerToStream(pStream, writePos, val, nBits);
					}

					pArray.AddInt("nReserved", 0);
					pArray.AddInt("nUncompressedLength", nSize);
					pArray.AddInt("nCompression", nCompression);
					pArray.AddInt("nCompressedLength", buffer.Count);
					pArray.AddInt("nAdjust", nAdjust);
					pArray.AddBytes("bytes", pStream);
				}
			}
			return pXStruct;
		}

		private int GetInteger(byte[] buffer, ref int pos, int nBits)
		{
			int nVal = 0;
			switch (nBits)
			{
				case 8:
					nVal = buffer[pos];
					pos += sizeof(byte);
					break;
				case 16:
					nVal = BitConverter.ToInt16(buffer, pos);
					pos += sizeof(short);
					break;
				case 32:
				default:
					nVal = BitConverter.ToInt32(buffer, pos);
					pos += sizeof(int);
					break;
			}
			return nVal;
		}

		private int WriteIntegerToStream(byte[] buffer, int pos, int nVal, int nBits)
		{
			switch (nBits)
			{
				case 8:
					buffer[pos] = (byte)nVal;
					return pos + sizeof(byte);
				case 16:
					BitConverter.GetBytes((short)nVal).CopyTo(buffer, pos);
					return pos + sizeof(short);
				case 32:
				default:
					BitConverter.GetBytes(nVal).CopyTo(buffer, pos);
					return pos + sizeof(int);
			}
		}

		private string ReadNullTerminatedString(BinaryReader reader)
		{
			List<byte> bytes = new List<byte>();
			byte b;
			while ((b = reader.ReadByte()) != 0)
				bytes.Add(b);
			return Encoding.UTF8.GetString(bytes.ToArray());
		}
	}
}