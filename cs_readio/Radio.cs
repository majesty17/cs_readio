using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using System.Collections.ObjectModel;
using NAudio.Wave;

namespace cs_readio
{
    public class Radio : IDisposable
    {
        static readonly ReadOnlyCollection<string> metadataSongPatterns = new ReadOnlyCollection<string>(new string[]
        {
            @"StreamTitle='(?<title>[^~]+?) - (?<artist>[^~;]+?)?'",
            @"StreamTitle='(?<title>.+?)~(?<artist>.+?)~"
        });

        public string Url
        {
            get;
            private set;
        }
        public bool Running
        {
            get
            {
                return _running;
            }
            set
            {
                _running = value;
                if (!_running && runningTask != null)
                    runningTask.Wait();
            }
        }
        bool _running;
        Task runningTask;
        Task textTask;

        Dictionary<string, string> task_queue = new Dictionary<string, string>();

        public string Metadata
        {
            get
            {
                return _metadata;
            }
            private set
            {
                if (OnMetadataChanged != null)
                    OnMetadataChanged(this, new MetadataEventArgs(_metadata, value));
                _metadata = value;
            }
        }
        string _metadata;
        public event EventHandler<MetadataEventArgs> OnMetadataChanged;


        public event EventHandler<StreamUpdateEventArgs> OnStreamUpdate;

        public event EventHandler<StreamOverEventArgs> OnStreamOver;


        public static event EventHandler<MessageLogEventArgs> OnMessageLogged;

        private string last_filename = null;


        AudioPlugin audioplugin;
        public Radio(string Url)
        {
            this.Url = Url;
            audioplugin = new AudioPlugin();
        }

        public void Start(string pluginsPath = null)
        {


            OnStreamUpdate += audioplugin.OnStreamUpdate; //pluginManager.OnStreamUpdate;
            OnStreamOver += audioplugin.OnStreamOver;
            Running = true;
            last_filename = null;
            runningTask = Task.Run(() => GetHttpStream());
            textTask = Task.Run(() => Voice2TextTask());
        }

        void GetHttpStream()
        {
            
            do
            {
                Console.WriteLine("into GetHttpStream");
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);
                    request.Headers.Add("icy-metadata", "1");
                    request.ReadWriteTimeout = 10 * 1000;
                    request.Timeout = 10 * 1000;
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        //get the position of metadata
                        int metaInt = 0;
                        if (!string.IsNullOrEmpty(response.GetResponseHeader("icy-metaint")))
                            metaInt = Convert.ToInt32(response.GetResponseHeader("icy-metaint"));
                        Console.WriteLine("metaInt is :" + metaInt);

                        using (Stream socketStream = response.GetResponseStream())
                        {
                            byte[] buffer = new byte[16384];
                            int metadataLength = 0;
                            int streamPosition = 0;
                            int bufferPosition = 0;
                            int readBytes = 0;
                            StringBuilder metadataSb = new StringBuilder();

                            while (Running)
                            {
                                if (bufferPosition >= readBytes)
                                {
                                    //wanto to read len,but not that much
                                    readBytes = socketStream.Read(buffer, 0, buffer.Length);
                                    bufferPosition = 0;
                                    //Console.WriteLine("Read byte: " + readBytes);
                                }
                                if (readBytes <= 0)
                                {
                                    Radio.Log("Stream over", this);
                                    Console.WriteLine("Read byte: nothing");
                                    break;
                                }

                                if (metadataLength == 0)
                                {
                                    if (metaInt == 0 || streamPosition + readBytes - bufferPosition <= metaInt)
                                    {
                                        streamPosition += readBytes - bufferPosition;
                                        ProcessStreamData(buffer, ref bufferPosition, readBytes - bufferPosition);
                                        continue;
                                    }

                                    ProcessStreamData(buffer, ref bufferPosition, metaInt - streamPosition);
                                    metadataLength = Convert.ToInt32(buffer[bufferPosition++]) * 16;
                                    //check if there's any metadata, otherwise skip to next block
                                    if (metadataLength == 0)
                                    {
                                        streamPosition = Math.Min(readBytes - bufferPosition, metaInt);
                                        ProcessStreamData(buffer, ref bufferPosition, streamPosition);
                                        continue;
                                    }
                                }
                                Console.WriteLine("bufferPosition is " + bufferPosition);
                                //get the metadata and reset the position,hardly in here
                                while (bufferPosition < readBytes)
                                {
                                    Console.WriteLine("in the inner while...");
                                    metadataSb.Append(Convert.ToChar(buffer[bufferPosition++]));
                                    metadataLength--;
                                    if (metadataLength == 0)
                                    {
                                        Metadata = metadataSb.ToString();
                                        metadataSb.Clear();
                                        streamPosition = Math.Min(readBytes - bufferPosition, metaInt);
                                        ProcessStreamData(buffer, ref bufferPosition, streamPosition);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (IOException ex)
                {
                    Radio.Log(string.Format("Handled IOException, reconnecting. Details:\n{0}\n{1}", ex.Message, ex.StackTrace), this);
                    OnStreamOver?.Invoke(this, new StreamOverEventArgs());
                }
                catch (SocketException ex)
                {
                    Radio.Log(string.Format("Handled SocketException, reconnecting. Details:\n{0}\n{1}", ex.Message, ex.StackTrace), this);
                    OnStreamOver?.Invoke(this, new StreamOverEventArgs());
                }
                catch (WebException ex)
                {
                    Radio.Log(string.Format("Handled WebException, reconnecting. Details:\n{0}\n{1}", ex.Message, ex.StackTrace), this);
                    OnStreamOver?.Invoke(this, new StreamOverEventArgs());
                }
            } while (Running);
        }



        void ProcessStreamData(byte[] buffer, ref int offset, int length)
        {
            //if return ,then no sound!!!
            //Console.WriteLine("in processStreamData():offset=" + offset + " ;len=" + length);
            if (length < 1)
                return;
            if (OnStreamUpdate != null)
            {
                byte[] data = new byte[length];
                Buffer.BlockCopy(buffer, offset, data, 0, length);
                OnStreamUpdate(this, new StreamUpdateEventArgs(data)); //no run no sound!!!
                writeBigFile(buffer, offset, length);
            }
            offset += length;
        }

        public static void Log(string Log, object sender)
        {
            if (OnMessageLogged != null)
                OnMessageLogged(sender, new MessageLogEventArgs(Log));
        }

        IntPtr _disposed = IntPtr.Zero;
        public void Dispose()
        {
            // Thread-safe single disposal
            if (Interlocked.Exchange(ref _disposed, (IntPtr)1) != IntPtr.Zero)
                return;

            Running = false;
            OnStreamUpdate -= audioplugin.OnStreamUpdate;
            OnStreamOver -= audioplugin.OnStreamOver;
            audioplugin.Dispose();
            audioplugin = null;
            OnMessageLogged = null;
        }

        public void Stop()
        {
            Dispose();
        }

        void Voice2TextTask()
        {
            while (Running) {
                //list wav files
                foreach (var item in task_queue) {
                    string file = item.Key;
                    string value = item.Value;
                    
                    if (value == null) {
                        string ret= Voice2Text.GetText(file);

                        task_queue[file] = ret;
                        Console.WriteLine(file + "," + ret);
                        break;
                    }
                }
            }
        }



        public void setVolumn(float vol) {
            if (audioplugin != null)
            {
                audioplugin.setVolumn(vol);
            }
        }


        //stream to file
        private void writeFile(byte[] data,int len) {
            string datadir = Directory.GetCurrentDirectory() + "\\data";
            if (Directory.Exists(datadir) == false)
                try
                {
                    Directory.CreateDirectory(datadir);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("Handled IOException, reconnecting. Details:\n{0}\n{1}", ex.Message, ex.StackTrace));
                }
            DateTime now = DateTime.Now;
            string filename = datadir + "\\" + (now.ToString("yyyyMMddhhmmss")) + "." + string.Format("{0:D3}", now.Millisecond);
            string filename_wav = filename + ".wav";
            FileStream fs = new FileStream(filename, FileMode.Create);
            fs.Write(data, 0, len);
            fs.Close();
            /*  using (Mp3FileReader reader = new Mp3FileReader(filename))
              {
                  WaveFileWriter.CreateWaveFile(filename_wav, reader);
              }*/
            try
            {
                var mp3Stream = new MemoryStream(data, 0, len);
                var mp3FileReader = new Mp3FileReader(mp3Stream);
                WaveFileWriter.CreateWaveFile(filename_wav, mp3FileReader);
            }
            catch (Exception ex) {
                Console.WriteLine(string.Format("mp3 2 wav error, Details:\n{0}\n{1}", ex.Message, ex.StackTrace));
            }
        }
        //steam to big file:every 5 seconds a big file
        private void writeBigFile(byte[] data, int offset, int len)
        {
            string datadir = Directory.GetCurrentDirectory() + "\\data";
            if (Directory.Exists(datadir) == false)
                try
                {
                    Directory.CreateDirectory(datadir);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("Handled IOException, reconnecting. Details:\n{0}\n{1}", ex.Message, ex.StackTrace));
                }
            DateTime now = DateTime.Now;
            string filename = datadir + "\\" + (now.ToString("yyyyMMddHHmm")) +
                string.Format("-{0:D2}", (now.Second / 5)) + ".data";
            FileStream fs = new FileStream(filename, FileMode.Create | FileMode.Append);
            fs.Write(data, offset, len);
            fs.Close();

            //如果文件名变了，则解析上一个，然后放进dic里。
            if (null != last_filename && filename.Equals(last_filename) == false)
            {
                try
                {
                    Mp3FileReader reader = new Mp3FileReader(last_filename);
                    string filename_wav = last_filename + ".wav";
                    WaveFileWriter.CreateWaveFile(filename_wav, reader);
                    task_queue.Add(filename_wav, null);
                }
                catch (Exception ex) { }
            }

            last_filename = filename;

        }

    }



    public class MetadataEventArgs : EventArgs
    {
        public string OldMetadata { get; private set; }
        public string NewMetadata { get; private set; }

        public MetadataEventArgs(string OldMetadata, string NewMetadata)
        {
            this.OldMetadata = OldMetadata;
            this.NewMetadata = NewMetadata;
        }
    }



    public class StreamUpdateEventArgs : EventArgs
    {
        public byte[] Data { get; private set; }

        public StreamUpdateEventArgs(byte[] Data)
        {
            this.Data = Data;
        }
    }

    public class StreamOverEventArgs : EventArgs
    {
        public StreamOverEventArgs()
        {
        }
    }

    public class MessageLogEventArgs : EventArgs
    {
        public string Message { get; private set; }

        public MessageLogEventArgs(string Message)
        {
            this.Message = Message;
        }
    }


}
