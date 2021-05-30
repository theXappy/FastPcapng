using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace FastPcapng
{
    public class WiresharkPipeSender
    {
        public Task SendPcapngAsync(string pipeName, MemoryPcapng pcapng)
        {
            return Task.Run((Action)(() =>
            {
                using NamedPipeServerStream toWireshark = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                toWireshark.WaitForConnection();
                pcapng.WriteTo(toWireshark);
                toWireshark.Flush(); // Really important 
                toWireshark.Close();
            }));
        }

        public void SendPcapng(string pipeName, byte[] pcapngBytes)
        {
            using NamedPipeServerStream toWireshark = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            toWireshark.WaitForConnection();
            toWireshark.Write(pcapngBytes);
            toWireshark.Flush();
            toWireshark.Close();
        }

        public void SendPcapng(string pipeName, Stream stream)
        {
            using NamedPipeServerStream toWireshark = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            toWireshark.WaitForConnection();
            stream.CopyTo(toWireshark);
            toWireshark.Flush();
            toWireshark.Close();
        }
    }
}
