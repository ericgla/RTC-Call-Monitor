using System;
using System.IO;
using System.Net;

namespace CallMonitor
{
    public class IPHeader
    {
        private byte _protocol;                 //Eight bits for the underlying protocol
        private uint _sourceIPAddress;          //Thirty two bits for the source IP Address
        private uint _destinationIPAddress;     //Thirty two bits for destination IP Address
                                                 
        private IPAddress _localIp;

        public IPHeader(byte[] buffer, int length, IPAddress localIp)
        {
            _localIp = localIp;

            try
            {
                //Create MemoryStream out of the received bytes
                MemoryStream memoryStream = new MemoryStream(buffer, 0, length);
                //Next we create a BinaryReader out of the MemoryStream
                BinaryReader binaryReader = new BinaryReader(memoryStream);

                //The first eight bits of the IP header contain the version and
                //header length so we read them
                _ = binaryReader.ReadByte();

                //The next eight bits contain the Differentiated services
                _ = binaryReader.ReadByte();

                //Next sixteen bits hold the total length of the datagram
                _ = (ushort)IPAddress.NetworkToHostOrder(binaryReader.ReadInt16());

                //Next sixteen have the identification bytes
                _ = (ushort)IPAddress.NetworkToHostOrder(binaryReader.ReadInt16());

                //Next sixteen bits contain the flags and fragmentation offset
                _ = (ushort)IPAddress.NetworkToHostOrder(binaryReader.ReadInt16());

                //Next eight bits have the TTL value
                _ = binaryReader.ReadByte();

                //Next eight represnts the protocol encapsulated in the datagram
                _protocol = binaryReader.ReadByte();

                //Next sixteen bits contain the checksum of the header
                _ = IPAddress.NetworkToHostOrder(binaryReader.ReadInt16());

                //Next thirty two bits have the source IP address
                _sourceIPAddress = (uint)(binaryReader.ReadInt32());

                //Next thirty two hold the destination IP address
                _destinationIPAddress = (uint)(binaryReader.ReadInt32());
            }
            catch { }
        }

        public bool IsTCP => _protocol == 6;

        public bool IsUDP => _protocol == 17;

        public IPAddress SourceAddress => new(_sourceIPAddress);

        public IPAddress DestinationAddress => new(_destinationIPAddress);

        // Addresses starting with a number between 224 and 239 are used for IP multicast
        public bool IsMulticast => DestinationAddress.GetAddressBytes()[0] >= 224 && DestinationAddress.GetAddressBytes()[0] <= 239;

        public bool IsBroadcast => DestinationAddress.ToString() == "255.255.255.255";

        public bool Inbound => DestinationAddress.Equals(_localIp);
    }
}