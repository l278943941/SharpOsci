namespace SharpOsci
{
    using NAudio.Wave;
    using NAudio.Flac;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;
    using System.Diagnostics;
    using shaderc;
    using System.Runtime.InteropServices;

    public partial class Form1 : Form
    {
        private WaveOutEvent _waveOut;      // 音频播放器
        private WaveStream miuscFileRead;
        private WaveStream miuscFileRead2;
        private FlacReader _flacreader;// 音频解码器用于播放 
        private FlacReader _flacreader2;// 音频解码器用于渲染
        private float[] _audioData;         // 存储解码后的音频数据
        private Timer _playbackTimer;       // 播放进度刷新定时器

        private List<float> _leftChannel = new List<float>();  // 左声道数据（X轴）
        private List<float> _rightChannel = new List<float>(); // 右声道数据（Y轴)
        private List<byte> bytedate = new List<byte>(); // 右声道数据（Y轴）

        // XY模式渲染器
        private XYWaveformRenderer _xyRenderer;
        Button open;
        Button play;

        private sdlRander _sdlWindow;
        private Panel _panel = new Panel { Dock = DockStyle.Fill };
        darwSetings darwSetings;

        Thread playert;

        [DllImport("kernel32.dll")]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        public static extern bool FreeConsole();
        public Form1()
        {
            SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
              ControlStyles.Opaque, true);
            ShaderCompiler p = new ShaderCompiler();
            byte[] chadercode = p.CompileComputeShader("Oscilloscope.comp", ShaderKind.ComputeShader);
            File.WriteAllBytes("Oscilloscope.comp.spv", chadercode);
            byte[] chadercode1 = p.CompileComputeShader("FullscreenQuad.frag.glsl", ShaderKind.FragmentShader);
            File.WriteAllBytes("FullscreenQuad.frag.spv", chadercode1);
            byte[] chadercode2 = p.CompileComputeShader("FullscreenQuad.vert.glsl", ShaderKind.VertexShader);
            File.WriteAllBytes("FullscreenQuad.vert.spv", chadercode2);
            AllocConsole();
            InitializeComponent();
            InitializeUI();
            InitializeAudio();
            
        }

        private void InitializeUI()
        {
            // 创建波形显示控件
            _xyRenderer = new XYWaveformRenderer { Dock = DockStyle.Fill };
            //new XYWaveformRenderer { Dock = DockStyle.Fill };
            //_xyRenderer._renderer = _sdlWindow;
            // 创建播放控制按钮
            open = new Button { Text = "打开文件", Dock = DockStyle.Top };
            open.Click += BtnOpen_Click;

            play = new Button { Text = "播放/暂停", Dock = DockStyle.Top };
            play.Click += BtnPlay_Click;

            // 布局
            var panel = new Panel { Dock = DockStyle.Top, Height = 40 };
            panel.Controls.Add(open);
            panel.Controls.Add(play);

            Controls.Add(_xyRenderer);
            Controls.Add(panel);

            darwSetings = _xyRenderer.darwsetings;


        }

        private void InitializeAudio()
        {
            _waveOut = new WaveOutEvent();
            _waveOut.PlaybackStopped += (s, e) => _playbackTimer.Stop();
        }

        // 打开音频文件并解码
        private void BtnOpen_Click(object sender, EventArgs e)
        {
            using (var openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "音频文件|*.flac;*.wav";
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    LoadAudioFile(openDialog.FileName);
                }
            }
        }

        private void LoadAudioFile(string path)
        {
            _flacreader?.Dispose();
            // 清空旧数据
            _leftChannel.Clear();
            _rightChannel.Clear();

            _waveOut = new WaveOutEvent();
            _flacreader = new FlacReader(path);
            _flacreader2= new FlacReader(path);

            _waveOut.Init((IWaveProvider)_flacreader);


            darwSetings.miuscBitDepth = (byte)(_flacreader.WaveFormat.BitsPerSample / 8);



            //_xyRenderer.SetSetings(darwSetings);

            // 读取全部音频数据到内存（确保精确度）

            float[] buffer = new float[_flacreader.WaveFormat.SampleRate * 2]; //1秒时长的双声道缓冲区
            byte[] buffer2 = new byte[_flacreader.WaveFormat.SampleRate * 2*3]; //1秒时长的双声道缓冲区

            Console.WriteLine($"Buffer Size: {buffer.Length}");

            int samplesRead = 0;
            int readpos = 0;

            List<float> bytedate2 = new List<float>();

            //while ((samplesRead = _flacreader.Read2(buffer2, readpos, buffer.Length)) > 0)
            //{

                
            //                    readpos += samplesRead;


            //                    for (int i = 0; i < samplesRead; i++)
            //                    {
            //                        bytedate.Add(buffer2[i]);
            //                    }
            //                    ////File.WriteAllBytes("miusc_dump.raw", bytedate.ToArray());

            //    //readpos += samplesRead;
            //    //bytedate2.AddRange(buffer);



            //    //byte[] file = File.ReadAllBytes("miusc_dump.raw");
            //    //float[] floats = new float[file.Length / 3];
            //    //ConvertBytesToFloat(file, 0, file.Length, floats, 0, 3);

            //    //int minlen = Math.Min(samplesRead, floats.Length);

            //    //for (int i = 0; i < minlen; i++)
            //    //{
            //    //    float a = floats[i];
            //    //    float b = bytedate2[i];
            //    //    if (a != b)
            //    //    {
            //    //        return;
            //    //    }
            //    //}


            //    ////bytedate.AddRange(buffer2);
            //    //return;


            //    //for (int i = 0; i < samplesRead; i += 2)
            //    //{
            //    //    if (i + 1 < samplesRead)
            //    //    {
            //    //        _leftChannel.Add(buffer[i]);     // 左声道（X轴）
            //    //        _rightChannel.Add(buffer[i + 1]); // 右声道（Y轴)
            //    //    }
            //    //}
            //}


            _xyRenderer.SetData2(bytedate.ToArray(), bytedate.Count);

        }


        private unsafe void ConvertBytesToFloat(byte[] source, int srcOffset, int srcCount, float[] dest, int destOffset, int bytesPerSample)
        {
            int maxSamples = srcCount / bytesPerSample;
            fixed (byte* p = source)
            {
                for (int i = 0; i < maxSamples; i++)
                {
                    int bytePos = srcOffset + i * bytesPerSample;
                    int sample = 0;

                    // 根据位深解析整数样本（假设小端字节序）
                    switch (bytesPerSample)
                    {
                        case 2: // 16-bit
                            sample = BitConverter.ToInt16(source, bytePos);
                            dest[destOffset + i] = sample / 32768f;
                            break;
                        case 3: // 24-bit
                                // 24-bit样本需要特殊处理，因为它不是直接的Int32

                            sample = ((*(int*)(p + bytePos)) << 8) >> 8;// 右移8位以适应float范围
                            dest[destOffset + i] = sample / 8388608f; // 除以2^31
                            break;
                        case 4: // 32-bit
                            sample = BitConverter.ToInt32(source, bytePos);
                            dest[destOffset + i] = sample / 2147483648f;
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported bit depth: {bytesPerSample * 8}-bit");
                    }
                }
            }
        }


        // 播放/暂停控制
        private void BtnPlay_Click(object sender, EventArgs e)
        {
            if (_flacreader == null)
            {
                MessageBox.Show("请先打开音频文件");
                return;
            }
            if (_waveOut == null)
            {
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_flacreader);
            }
            _xyRenderer.isplayer = true;

            if (_waveOut.PlaybackState == PlaybackState.Playing)
            {
                _waveOut.Pause(); 
                ((Button)sender).Text = "播放";
                _xyRenderer.isplayer = false;
            }
            else
            {
                _flacreader.Position = 0;
                ((Button)sender).Text = "暂停";
                _xyRenderer.isplayer = true;

                _waveOut.Play();
                cuttentposition = 0;
                StartPlaybackSync(); // 启动播放同步刷新
            }
        }
     
     int cuttentposition = 0;

        // 实时同步音频播放位置与XY显示
    private void StartPlaybackSync()
        {
            byte[] buffer = new byte[(_flacreader.WaveFormat.SampleRate / 60) * 2 * (_flacreader2.WaveFormat.BitsPerSample / 8) * 10];

            playert = new Thread(() => {
                float farmexishu = 60;
                int lestsubtime = 0;

                int readpos = 0;
                int lastposition = 0;
                long last_flacreaderPosition = 0;

                while (true)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                   
                    if (_flacreader == null || _waveOut.PlaybackState != PlaybackState.Playing)
                    {
                        cuttentposition = 0;
                        readpos = 0;
                        _xyRenderer.isplayer = false;
                        _flacreader2.Position = 0;
                        //this.Close();
                        return;
                    }
                    cuttentposition += (int) ( _flacreader.WaveFormat.SampleRate / farmexishu*2* (_flacreader2.WaveFormat.BitsPerSample / 8));//单声道切片的float长度
                    if (cuttentposition < (_flacreader.Position)&& last_flacreaderPosition!= _flacreader.Position)
                    {
                        cuttentposition += (int)((_flacreader.Position) - cuttentposition) / 2;
                        farmexishu+= ((_flacreader.Position - cuttentposition)/ cuttentposition)*0.5f;
                    }
                    else if (cuttentposition > _flacreader.Position&& last_flacreaderPosition != _flacreader.Position)
                    {
                        farmexishu += ((_flacreader.Position  - cuttentposition) / cuttentposition) * 0.5f;
                    }
                    if (last_flacreaderPosition != _flacreader.Position) {
                        last_flacreaderPosition = _flacreader.Position;
                    }


                    _xyRenderer.Invoke(() =>
                    {
                        Stopwatch sw = Stopwatch.StartNew();

                        //Debug.WriteLine(cuttentposition);

                        //int readlen = ((cuttentposition - lastposition) * 2 * (_flacreader.WaveFormat.BitsPerSample / 8));
                        //int samplesRead = _flacreader.Read(buffer, 0, readlen);
                        //_xyRenderer.SetData2(buffer, Math.Min(samplesRead, readlen));
                        //lastposition = cuttentposition;

                        //_xyRenderer.SetPlayerPosition((long)(cuttentposition));

                        int samplesRead = _flacreader2.Read2(buffer, readpos,Math.Min(buffer.Length, cuttentposition- readpos));
                        if(samplesRead < (_flacreader.WaveFormat.SampleRate / 60) * 2 * darwSetings.miuscBitDepth)
                        {
                            return;
                        }
                        readpos += samplesRead;

                        //cpplib渲染模式 请使用release运行 并且注释 XYWaveformRenderer.cs 178行到181行
                        //_xyRenderer.SetData2(buffer, samplesRead);
                        //

                        //ilgpu渲染模式
                        _xyRenderer.ilgpuRender.SetData(buffer, samplesRead);
                        //

                        sw.Stop();
                        play.Text = "暂停 当前帧渲染时间:" + sw.ElapsedMilliseconds.ToString() + "系数:" + farmexishu.ToString();
                    });

                    Thread.Sleep((int)(Math.Max(0,((16- lestsubtime) - sw.ElapsedMilliseconds))));
                    lestsubtime -= (int)(Math.Max(lestsubtime, 16));
                    lestsubtime = Math.Max(0, lestsubtime);
                    if (sw.ElapsedMilliseconds > 16)
                    {
                        lestsubtime += (int)(sw.ElapsedMilliseconds - 16);
                    }
                }
            
            
            });
            playert.Start();

        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _waveOut?.Dispose();
            _flacreader?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
