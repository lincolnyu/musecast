using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MuseCast
{
    public class Mp3MulticastStream : Stream
    {
        #region Constants

        private const int DefaultAudioBufferFrameCount = 32;
        private const string HttpVersion = "1.1";
        
        #endregion

        #region Backing fields

        private long _length;

        #endregion

        byte[][] _audioBuffers;
        ReaderWriterLock[] _bufferLocks;
        int _currentWriting = 0;

        private readonly TcpListener _listener;
        private int _numRunningThreads;
        private readonly AutoResetEvent _doneEvent = new AutoResetEvent(false);
        private readonly AutoResetEvent _writtenEvent = new AutoResetEvent(false);

        public Mp3MulticastStream(IPAddress ipAddress, int port, int audioBufferFrameCount = DefaultAudioBufferFrameCount)
        {
            try
            {
                _audioBuffers = new byte[audioBufferFrameCount][];
                _bufferLocks = new ReaderWriterLock[audioBufferFrameCount];

                //start listing on the given port
                _listener = new TcpListener(ipAddress, port);
                _listener.Start();

                Console.WriteLine("Mp3MultiCastStream listening at {0}:{1}... Press ^C to stop...", ipAddress, port);

                //start the thread which calls the method 'StartListen'
                Fork();

                while (true)
                {
                    if (!_doneEvent.WaitOne()) continue;
                    if (_numRunningThreads <= 0)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An exception occurred while listening: " + e);
                throw e;
            }
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length
        {
            get { return _length; }
        }

        public override long Position
        {
            get; set;
        }

        public override void Flush()
        {
            
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _bufferLocks[_currentWriting].AcquireWriterLock(-1);
            // TODO optimize
            var buf = _audioBuffers[_currentWriting] = new byte[count];
            var j = 0;
            for (var i = offset; i < count; i++, j++)
            {
                buf[j] = buffer[i];
            }
            _currentWriting++;
            _writtenEvent.Set();
        }

        private void TryStartListen(object obj)
        {
            Socket socket = null;
            try
            {
                do
                {
                    // accepts a new connection
                    socket = _listener.AcceptSocket();
                    Console.WriteLine("Socket type " + socket.SocketType);
                } while (!socket.Connected);

                Fork();

                Stream(socket);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error occurred: {0} ", e);
            }
            finally
            {
                socket?.Close();
                Merge();
            }
        }

        private void Fork()
        {
            ThreadPool.QueueUserWorkItem(TryStartListen);
            Interlocked.Increment(ref _numRunningThreads);
        }

        private void Merge()
        {
            Interlocked.Decrement(ref _numRunningThreads);
            _doneEvent.Set();
        }

        private void Stream(Socket socket)
        {
            SendHeader(HttpVersion, "audio/wav", -1, " 200 OK", socket);

            int currentReading = _currentWriting;
            while (true)
            {
                if (currentReading == _currentWriting)
                {
                    _writtenEvent.WaitOne();
                }
                for (; currentReading != _currentWriting; currentReading++)
                {
                    _bufferLocks[currentReading].AcquireReaderLock(-1);
                    var buf = _audioBuffers[currentReading];
                    SendToBrowser(buf, 0, buf.Length, socket);
                }
            }
        }

        /// <summary>
        /// This function send the Header Information to the client (Browser)
        /// </summary>
        /// <param name="httpVersion">HTTP Version</param>
        /// <param name="mimeHeader">Mime Type</param>
        /// <param name="totalBytes">Total Bytes to be sent in the body</param>
        /// <param name="statusBytes"></param>
        /// <param name="socket">Socket reference</param>
        /// <returns></returns>
        public void SendHeader(string httpVersion, string mimeHeader, int totalBytes, string statusBytes, Socket socket)
        {
            var sBuffer = "";

            // if Mime type is not provided set default to text/html
            if (mimeHeader.Length == 0)
            {
                mimeHeader = "text/html"; // Default Mime Type is text/html
            }

            sBuffer = sBuffer + httpVersion + statusBytes + "\r\n";
            sBuffer = sBuffer + "Server: cx1193719-b\r\n";
            sBuffer = sBuffer + "Content-Type: " + mimeHeader + "\r\n";
            sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";
            if (totalBytes >= 0)
            {
                sBuffer = sBuffer + "Content-Length: " + totalBytes + "\r\n\r\n";
            }

            Console.WriteLine("=== header ===");
            Console.WriteLine(sBuffer);
            Console.WriteLine("==============");

            SendToBrowser(sBuffer, socket);
        }
        
        /// <summary>
        ///  Overloaded Function, takes string, convert to bytes and calls 
        ///  overloaded sendToBrowserFunction.
        /// </summary>
        /// <param name="sData">The data to be sent to the browser(client)</param>
        /// <param name="socket">Socket reference</param>
        public bool SendToBrowser(String sData, Socket socket)
        {
            return SendToBrowser(Encoding.UTF8.GetBytes(sData), socket);
        }

        public bool SendToBrowser(byte[] buffer, Socket socket)
        {
            return SendToBrowser(buffer, 0, buffer.Length, socket);
        }

        /// <summary>
        ///  Sends data to the browser (client)
        /// </summary>
        /// <param name="buffer">Byte Array</param>
        /// <param name="offset">The position in the data buffer at which to begin sending data</param>
        /// <param name="length">The number of bytes to send</param>
        /// <param name="socket">Socket reference</param>
        public bool SendToBrowser(byte[] buffer, int offset, int length, Socket socket)
        {
            try
            {
                if (socket.Connected)
                {
                    if ((socket.Send(buffer, offset, length, 0)) == -1)
                    {
                        Console.WriteLine("Socket Error cannot Send Packet");
                    }
                    else
                    {
                        Console.Write("S");
                        //Console.WriteLine("No. of bytes sent {0}", numBytes);
                    }
                    return true;
                }
                Console.WriteLine("Connection Dropped....");
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Occurred: {0} ", e);
            }
            return false;
        }
    }
}
