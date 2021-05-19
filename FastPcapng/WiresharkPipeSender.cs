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
                NamedPipeServerStream toWireshark = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                Debug.WriteLine(" &&& NamedPipe Waiting for connection");
                toWireshark.WaitForConnection();
                Debug.WriteLine(" &&& NamedPipe Connected! Writing data");
                pcapng.WriteTo(toWireshark);
                Debug.WriteLine(" &&& NamedPipe Wrote Data! Sleeping...");

                Thread.Sleep(3_00);
                Debug.WriteLine(" &&& NamedPipe Slept");
                Debug.WriteLine(" &&& NamedPipe Closing");

                toWireshark.Close();
                toWireshark.Dispose();
            }));
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
