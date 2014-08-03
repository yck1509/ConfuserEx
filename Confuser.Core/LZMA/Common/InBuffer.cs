// InBuffer.cs

using System;
using System.IO;

namespace SevenZip.Buffer {
	internal class InBuffer {

		private readonly byte[] m_Buffer;
		private readonly uint m_BufferSize;
		private uint m_Limit;
		private uint m_Pos;
		private ulong m_ProcessedSize;
		private Stream m_Stream;
		private bool m_StreamWasExhausted;

		public InBuffer(uint bufferSize) {
			m_Buffer = new byte[bufferSize];
			m_BufferSize = bufferSize;
		}

		public void Init(Stream stream) {
			m_Stream = stream;
			m_ProcessedSize = 0;
			m_Limit = 0;
			m_Pos = 0;
			m_StreamWasExhausted = false;
		}

		public bool ReadBlock() {
			if (m_StreamWasExhausted)
				return false;
			m_ProcessedSize += m_Pos;
			int aNumProcessedBytes = m_Stream.Read(m_Buffer, 0, (int)m_BufferSize);
			m_Pos = 0;
			m_Limit = (uint)aNumProcessedBytes;
			m_StreamWasExhausted = (aNumProcessedBytes == 0);
			return (!m_StreamWasExhausted);
		}


		public void ReleaseStream() {
			// m_Stream.Close(); 
			m_Stream = null;
		}

		public bool ReadByte(byte b) // check it
		{
			if (m_Pos >= m_Limit)
				if (!ReadBlock())
					return false;
			b = m_Buffer[m_Pos++];
			return true;
		}

		public byte ReadByte() {
			// return (byte)m_Stream.ReadByte();
			if (m_Pos >= m_Limit)
				if (!ReadBlock())
					return 0xFF;
			return m_Buffer[m_Pos++];
		}

		public ulong GetProcessedSize() {
			return m_ProcessedSize + m_Pos;
		}

	}
}