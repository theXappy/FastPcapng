using System.IO;
using System.IO.Pipes;

namespace FastPcapng
{
    public class WiresharkPipeSender
    {
        public void SendPcapng(string pipeName, MemoryPcapng pcapng)
        {
            NamedPipeServerStream toWireshark = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            toWireshark.WaitForConnection();
            pcapng.WriteTo(toWireshark);
            toWireshark.Close();
        }

        public void SendPcapng(string pipeName, byte[] pcapngBytes)
        {
            NamedPipeServerStream toWireshark = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            toWireshark.WaitForConnection();
            toWireshark.Write(pcapngBytes);
            toWireshark.Close();
        }

        public void SendPcapng(string pipeName, Stream stream)
        {
            NamedPipeServerStream toWireshark = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            toWireshark.WaitForConnection();
            stream.CopyTo(toWireshark);
            toWireshark.Close();
        }
    }
}
