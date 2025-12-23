using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace zeroconf.zeroconf
{
    public class Packet
    {
        private static int FLAG_RESPONSE = 15;
        private static int FLAG_AA = 10;
        private List<Record> _questions;
        private List<Record> _answers;
        private List<Record> _authorities;
        private List<Record> _additionals;
        private int _id;
        private int _flags;
        private IPEndPoint _address;

        internal Packet() : this(0)
        {
        }

        internal Packet(int id)
        {
            _id = id;
            _questions = new List<Record>();
            _answers = new List<Record>();
            _authorities = new List<Record>();
            _additionals = new List<Record>();
            SetResponse(true);
        }

        internal IPEndPoint GetAddress()
        {
            return _address;
        }

        internal void SetAddress(IPEndPoint address)
        {
            _address = address;
        }

        internal int GetId()
        {
            return _id;
        }

        /// <summary>
        /// Return true if it's a response, false if it's a query
        /// </summary>
        internal bool IsResponse()
        {
            return IsFlag(FLAG_RESPONSE);
        }

        internal void SetResponse(bool on)
        {
            SetFlag(FLAG_RESPONSE, on);
        }

        internal bool IsAuthoritative()
        {
            return IsFlag(FLAG_AA);
        }

        internal void SetAuthoritative(bool on)
        {
            SetFlag(FLAG_AA, on);
        }

        private bool IsFlag(int flag)
        {
            return (_flags & (1 << flag)) != 0;
        }

        private void SetFlag(int flag, bool on)
        {
            if (on) _flags |= (1 << flag);
            else _flags &= ~(1 << flag);
        }

        internal void Read(BinaryReader reader, IPEndPoint address)
        {
            byte[] q = new byte[reader.BaseStream.Length - reader.BaseStream.Position];
            reader.ReadFully(q);
            reader.BaseStream.Position = 0;
            
            _address = address;
            _id = reader.ReadUInt16() & 0xFFFF;
            _flags = reader.ReadUInt16() & 0xFFFF;
            int numquestions = reader.ReadUInt16() & 0xFFFF;
            int numanswers = reader.ReadUInt16() & 0xFFFF;
            int numauthorities = reader.ReadUInt16() & 0xFFFF;
            int numadditionals = reader.ReadUInt16() & 0xFFFF;

            for (int i = 0; i < numquestions; i++)
            {
                _questions.Add(Record.ReadQuestion(reader));
            }

            for (int i = 0; i < numanswers; i++)
            {
                if (reader.BaseStream.Position < reader.BaseStream.Length) _answers.Add(Record.ReadAnswer(reader));
            }

            for (int i = 0; i < numauthorities; i++)
            {
                if (reader.BaseStream.Position < reader.BaseStream.Length) _authorities.Add(Record.ReadAnswer(reader));
            }

            for (int i = 0; i < numadditionals; i++)
            {
                if (reader.BaseStream.Position < reader.BaseStream.Length) _additionals.Add(Record.ReadAnswer(reader));
            }
        }

        internal void Write(BinaryWriter writer)
        {
            writer.WriteBigEndian((short) _id);
            writer.WriteBigEndian((short) _flags);
            writer.WriteBigEndian((short) _questions.Count);
            writer.WriteBigEndian((short) _answers.Count);
            writer.WriteBigEndian((short) _authorities.Count);
            writer.WriteBigEndian((short) _additionals.Count);
            foreach (var record in _questions) record.Write(writer, this);
            foreach (var record in _answers) record.Write(writer, this);
            foreach (var record in _authorities) record.Write(writer, this);
            foreach (var record in _additionals) record.Write(writer, this);
        }
        
        public override String ToString() {
            return "Packet(" +
                   "id=" + _id +
                   ", flags=" + _flags +
                   ", questions=" + _questions +
                   ", answers=" + _answers +
                   ", authorities=" + _authorities +
                   ", additionals=" + _additionals +
                   ", address=" + _address +
                   ')';
        }

        internal List<Record> GetQuestions()
        {
            return _questions;
        }

        internal List<Record> GetAnswers()
        {
            return _answers;
        }

        internal List<Record> GetAdditionals()
        {
            return _additionals;
        }

        internal void AddAnswer(Record record)
        {
            _answers.Add(record);
        }

        internal void AddQuestion(Record record)
        {
            _questions.Add(record);
        }

        internal void AddAdditional(Record record)
        {
            _additionals.Add(record);
        }

        internal void AddAuthority(Record record)
        {
            _authorities.Add(record);
        }
    }
}