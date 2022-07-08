namespace ProxyV2.Socks5
{
    public class Socks5Consts
    {
        public static byte Socks5 { get; set; } = 0x05;
        public static byte Socks4 { get; set; } = 0x04;
        public static byte WrongAuthMethod { get; set; } = 0xFF;
    }

    public enum Command : byte
    {
        SetTcpIp = 0x1,
        BindTcpIp = 0x2,
        AsociateUdp = 0x3
    }

    public enum AddressType : byte
    {
        IPv4 = 0x1,
        DomainName = 0x3,
        IPv6 = 0x4,
        Error
    }

    public enum Socks5ServerResponseStatus : byte
    {
        Ok,
        ErrorSocksServer,
        Denied,
        NetUnavailable,
        HostUnavailable,
        ConnectionFailure,
        TTLExpiration,
        CommandNotSupportedProtocolError,
        AddressTypeNotSupported
    }
}
