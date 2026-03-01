//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

using System;
using System.IO;
using System.ServiceModel.Channels;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace PKISharp.WACS.Plugins.ValidationPlugins.ArsysClient
{
    public class CustomTextMessageEncoder : MessageEncoder
    {
        private CustomTextMessageEncoderFactory factory;
        private XmlWriterSettings writerSettings;
        private string contentType;

        public CustomTextMessageEncoder(CustomTextMessageEncoderFactory factory)
        {
            this.factory = factory;

            this.writerSettings = new XmlWriterSettings();
            this.writerSettings.Encoding = Encoding.GetEncoding(factory.CharSet);
            this.contentType = string.Format("{0}; charset={1}",
                this.factory.MediaType, this.writerSettings.Encoding.HeaderName);
        }

        public override string ContentType
        {
            get
            {
                return this.contentType;
            }
        }

        public override string MediaType
        {
            get
            {
                return factory.MediaType;
            }
        }

        public override MessageVersion MessageVersion
        {
            get
            {
                return this.factory.MessageVersion;
            }
        }

        public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
        {
            byte[] msgContents = new byte[buffer.Count];
            Array.Copy(buffer.Array, buffer.Offset, msgContents, 0, msgContents.Length);
            bufferManager.ReturnBuffer(buffer.Array);

            MemoryStream stream = new MemoryStream(msgContents);
            return ReadMessage(stream, int.MaxValue);
        }

        public override Message ReadMessage(Stream stream, int maxSizeOfHeaders, string contentType)
        {
            /*
             * Code to modify raw XML response.
             * API says some vales cant be null, but in some scenarios it returns a nil value and this breaks the serializer.
             * So we modify the XML to remove the nil, and add a false as the bool value
             * Here you can attach a debuger stop point to see the raw XML
             */
            
            string xml;
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                xml = reader.ReadToEnd();
            }

            // Uncomment if neccesary
            //Console.WriteLine("----- Original Arsys XML -----");
            //Console.WriteLine(xml);
            //Console.WriteLine("-------------------------");
            string pattern = @"<(?<name>\w+)([^>]*)\s+xsi:nil\s*=\s*""true""([^>]*)/?>";

            xml = Regex.Replace(
                xml,
                pattern,
                m =>
                {
                    string name = m.Groups["name"].Value;
                    string allAttrs = m.Groups[1].Value + m.Groups[2].Value;

                    // remove xsi:nil
                    allAttrs = Regex.Replace(allAttrs, @"\s*xsi:nil\s*=\s*""true""", "").Trim();

                    // remove / at end if exists
                    allAttrs = allAttrs.TrimEnd('/');

                    // rebuild node
                    return $"<{name}{(allAttrs.Length > 0 ? " " + allAttrs : "")}>false</{name}>";
                },
                RegexOptions.IgnoreCase | RegexOptions.Multiline
            );

            // Uncomment if neccesary
            //Console.WriteLine("----- Modified Arsys XML -----");
            //Console.WriteLine(xml);
            //Console.WriteLine("-------------------------");

            var bytes = Encoding.UTF8.GetBytes(xml);
            var newStream = new MemoryStream(bytes);

            var xmlReader = XmlReader.Create(newStream);

            return Message.CreateMessage(xmlReader, maxSizeOfHeaders, this.MessageVersion);
        }

        public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
        {
            MemoryStream stream = new MemoryStream();
            XmlWriter writer = XmlWriter.Create(stream, this.writerSettings);
            message.WriteMessage(writer);
            writer.Close();

            byte[] messageBytes = stream.GetBuffer();
            int messageLength = (int)stream.Position;
            stream.Close();

            int totalLength = messageLength + messageOffset;
            byte[] totalBytes = bufferManager.TakeBuffer(totalLength);
            Array.Copy(messageBytes, 0, totalBytes, messageOffset, messageLength);

            ArraySegment<byte> byteArray = new ArraySegment<byte>(totalBytes, messageOffset, messageLength);
            return byteArray;
        }

        public override void WriteMessage(Message message, Stream stream)
        {
            XmlWriter writer = XmlWriter.Create(stream, this.writerSettings);
            message.WriteMessage(writer);
            writer.Close();
        }
    }
}