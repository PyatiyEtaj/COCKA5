namespace ProxyV2
{
    public class ServerConfiguration
    {
        public int ReconnectMaxTries { get; init; }
        public int ReconnectTimeoutMs { get; init; }
        public int CountOfTriesReadDataFromSocket { get; init; }
        public int TimeoutBetweenReadWriteSocketDataMs { get; init; }
        public int BufferSizeBytes { get; init; }
        public string Host { get; init; }
        public int Port { get; init; }
    }
}
