//
// AaltoTLS.RecordLayer.AsyncSendRecordsResult
//
// Authors:
//      Juho Vähä-Herttua  <juhovh@iki.fi>
//
// Copyright (C) 2010-2011  Aalto University
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace deps.AaltoTLS.RecordLayer
{
	public class AsyncSendRecordsResult : AaltoTLS.AsyncGenericResult
	{
		private Object _lock = new Object();
		private List<Record> _queue = new List<Record>();
		
		public AsyncSendRecordsResult(Record[] records, AsyncCallback requestCallback, Object state)
			: base(requestCallback, state)
		{
			_queue.AddRange(records);
		}
		
		public byte[] GetRecords(uint maxDataLength)
		{
			MemoryStream memStream = new MemoryStream();
			lock (_lock) {
				while (_queue.Count > 0) {
					byte[] recordBytes = _queue[0].GetBytes();
					if (maxDataLength > 0 && memStream.Length+recordBytes.Length > maxDataLength) {
						return memStream.ToArray();
					}
					_queue.RemoveAt(0);
					
					memStream.Write(recordBytes, 0, recordBytes.Length);
					memStream.Flush();
				}
			}
			return memStream.ToArray();
		}
	}
}
