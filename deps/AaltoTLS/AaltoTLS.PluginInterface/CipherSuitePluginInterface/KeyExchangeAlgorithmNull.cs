//
// AaltoTLS.PluginInterface.KeyExchangeAlgorithmNull
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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace deps.AaltoTLS.PluginInterface
{
	public class KeyExchangeAlgorithmNull: KeyExchangeAlgorithm
	{
		public override string CertificateKeyAlgorithm
		{
			get { return null; }
		}
		
		public override bool SupportsProtocolVersion(ProtocolVersion version)
		{
			return true;
		}

		public override byte[] GetServerKeys(ProtocolVersion version)
		{
			return null;
		}
		
		public override byte[] ProcessServerKeys(ProtocolVersion version, byte[] data)
		{
			throw new Exception("Server keys received in null key exchange");
		}

		public override byte[] GetClientKeys(ProtocolVersion version, ProtocolVersion clientVersion, CertificatePublicKey publicKey)
		{
			return null;
		}
		
		public override void ProcessClientKeys(ProtocolVersion version, ProtocolVersion clientVersion, CertificatePrivateKey privateKey, byte[] data)
		{
			throw new Exception("Client keys received in null key exchange");
		}

		public override byte[] GetMasterSecret(PseudoRandomFunction prf, byte[] seed)
		{
			throw new Exception("Master secret requested for null key exchange");
		}
	}
}
