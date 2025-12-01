using System;
using System.Diagnostics;
using System.IO;
using deps.HttpSharp;
using lib.common;
using lib.mercury;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using ProtoBuf;
using spotify.login5.v3;
using spotify.login5.v3.challenges;

namespace lib.core
{
    public class Login5Api
    {
        private Session _session;

        public Login5Api(Session session)
        {
            _session = session;
        }

        private static int NumberOfTrailingZeros(int value)
        {
            if (value == 0)
            {
                return 32;
            }

            int count = 0;
            while ((value & 1) == 0)
            {
                count++;
                value >>= 1;
            }

            return count;
        }

        private static bool CheckTenTrailingBits(byte[] array)
        {
            if (array[array.Length - 1] != 0) return false;
            else return NumberOfTrailingZeros(array[array.Length - 2]) >= 2;
        }
        
        private static void IncrementCtr(byte[] ctr, int index)
        {
            ctr[index]++;
            if (ctr[index] == 0 && index != 0)
                IncrementCtr(ctr, index - 1);
        }

        private static ChallengeSolve SolveHashCash(byte[] prefix, int length, byte[] random)
        {
            IDigest digest = new Sha1Digest();

            byte[] suffix = new byte[16];
            Buffer.BlockCopy(random, 0, suffix, 0, 8);

            if (length != 10)
            {
                throw new ArgumentException("Length must be 10.");
            }

            int iters = 0;
            while (true)
            {
                digest.Reset();
                digest.BlockUpdate(prefix, 0, prefix.Length);
                digest.BlockUpdate(suffix, 0, suffix.Length);
                byte[] hash = new byte[digest.GetDigestSize()];
                digest.DoFinal(hash, 0);
                if (CheckTenTrailingBits(hash))
                {
                    return new ChallengeSolve(suffix, iters);
                }

                IncrementCtr(suffix, suffix.Length - 1);
                IncrementCtr(suffix, 7);
                iters++;
            }
        }
        
        private static long NanoTime() {
            long nano = 10000L * Stopwatch.GetTimestamp();
            nano /= TimeSpan.TicksPerMillisecond;
            nano *= 100L;
            return nano;
        }

        private static LoginRequest SolveChallenge(LoginResponse resp)
        {
            byte[] loginContext = resp.LoginContext;

            HashcashChallenge hashcash = resp.Challenges.challenges[0].Hashcash;

            byte[] prefix = hashcash.Prefix;
            byte[] seed = new byte[8];
            IDigest digest = new Sha1Digest();
            digest.BlockUpdate(loginContext, 0, loginContext.Length);
            byte[] loginContextDigest = new byte[digest.GetDigestSize()];
            digest.DoFinal(loginContextDigest, 0);
            Buffer.BlockCopy(loginContextDigest, 12, seed, 0, 8);

            long start = NanoTime();
            ChallengeSolve solved = SolveHashCash(prefix, hashcash.Length, seed);
            long durationNano = NanoTime() - start;
            
            return new LoginRequest
            {
                LoginContext = loginContext,
                ChallengeSolutions = new ChallengeSolutions
                {
                    Solutions =
                    {
                        new ChallengeSolution
                        {
                            Hashcash = new HashcashSolution
                            {
                                Duration = TimeSpan.FromTicks(durationNano / 100),
                                Suffix = solved._suffix
                            }
                        }
                    }
                }
            };
        }

        private LoginResponse Send(LoginRequest msg)
        {
            HttpRequest req = new HttpRequest(new Uri("https://login5.spotify.com/v3/login"), HttpMethod.Post);
            req.ContentType = "application/x-protobuf";
            req.SetData(Utils.ProtoBytes(msg));

            Stream response = _session.GetClient().NewCall(req).GetResponseStream();
            if (response == null) throw new IOException("No body");
            return Serializer.Deserialize<LoginResponse>(response);
        }

        public LoginResponse Login5(LoginRequest req)
        {
            req.ClientInfo = new ClientInfo
            {
                ClientId = MercuryRequests.KEYMASTER_CLIENT_ID,
                DeviceId = _session.GetDeviceId()
            };
            
            LoginResponse resp = Send(req);
            if (resp.Challenges != null)
            {
                LoginRequest reqq = SolveChallenge(resp);
                reqq.LoginContext = req.LoginContext;
                reqq.ChallengeSolutions = req.ChallengeSolutions;
                reqq.ClientInfo = req.ClientInfo;
                reqq.AppleSignInCredential = req.AppleSignInCredential;
                reqq.FacebookAccessToken = req.FacebookAccessToken;
                reqq.OneTimeToken = req.OneTimeToken;
                reqq.PhoneNumber = req.PhoneNumber;
                reqq.StoredCredential = req.StoredCredential;
                reqq.Password = req.Password;
                
                resp = Send(reqq);
            }

            return resp;
        }
        
        private class ChallengeSolve
        {
            public byte[] _suffix;
            public int _ctr;

            public ChallengeSolve(byte[] suffix, int ctr)
            {
                _suffix = suffix;
                _ctr = ctr;
            }
        }
    }
}