using Microsoft.CognitiveServices.SpeechRecognition;
using System;
using System.IO;
using System.Windows;

namespace speech2text
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string SHORTWAVEFILE = "whatstheweatherlike.wav";
        const string LONGWAVEFILE = "batman.wav";
        const string SUBSCRIPTIONKEY = "c981de794f7f41bf8f44a76eeb8ced1a";

        /// <summary>
        /// 语音识别的客户端类型。
        /// </summary>
        private DataRecognitionClient dataClient;

        public MainWindow()
        {
            InitializeComponent();
        }

        #region EventHandler

        /// <summary>
        /// 执行 ShortPhrase 模式的语音识别，
        /// 这种模式最长支持 15 秒的语音。
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e"></param>
        private void ShortPhraseStartButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateUIControls();
            this.CreateDataRecoClient(SpeechRecognitionMode.ShortPhrase);
            this.SendAudioHelper(SHORTWAVEFILE);
        }

        /// <summary>
        /// 执行 LongDictation 模式的语音识别，
        /// 这种模式最长支持 120 秒的语音。
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e"></param>
        private void LongDictationStartButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateUIControls();
            this.CreateDataRecoClient(SpeechRecognitionMode.LongDictation);
            this.SendAudioHelper(LONGWAVEFILE);
        }

        /// <summary>
        /// 在 ShortPhrase 模式的识别完成后执行该函数。
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e"><see cref="SpeechResponseEventArgs"/>该类型的实例包含语音识别的结果。</param>
        private void OnDataShortPhraseResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                this.WriteLine("--- OnDataShortPhraseResponseReceivedHandler ---");

                // 如果是发送从麦克风中获得的数据，此时就可以关闭麦克风了。
                this.WriteResponseResult(e);

                this.ShortPhraseStartButton.IsEnabled = true;
                this.LongDictationStartButton.IsEnabled = true;
            }));
        }

        /// <summary>
        /// Called when a final response is received;
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechResponseEventArgs"/>该类型的实例包含语音识别的结果。</param>
        private void OnDataDictationResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            this.WriteLine("--- OnDataDictationResponseReceivedHandler ---");
            if (e.PhraseResponse.RecognitionStatus == RecognitionStatus.EndOfDictation ||
                e.PhraseResponse.RecognitionStatus == RecognitionStatus.DictationEndSilenceTimeout)
            {
                Dispatcher.Invoke(
                    (Action)(() =>
                    {
                        this.ShortPhraseStartButton.IsEnabled = true;
                        this.LongDictationStartButton.IsEnabled = true;

                        // 如果是发送从麦克风中获得的数据，此时就可以关闭麦克风了。
                    }));
            }

            this.WriteResponseResult(e);
        }

        /// <summary>
        /// 输出服务端返回的部分识别结果。
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PartialSpeechResponseEventArgs"/>该类型的实例包含语部分音识别的结果。</param>
        private void OnPartialResponseReceivedHandler(object sender, PartialSpeechResponseEventArgs e)
        {
            this.PartialResponseWriteLine("--- Partial result received by OnPartialResponseReceivedHandler() ---");
            this.PartialResponseWriteLine("{0}", e.PartialResult);
            this.PartialResponseWriteLine();
        }

        /// <summary>
        /// 输出信息告诉用户：出错了。
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechErrorEventArgs"/>该类型的实例包含服务端发生的错误信息。</param>
        private void OnConversationErrorHandler(object sender, SpeechErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                this.ShortPhraseStartButton.IsEnabled = true;
                this.LongDictationStartButton.IsEnabled = true;
            });

            this.WriteLine("--- Error received by OnConversationErrorHandler() ---");
            this.WriteLine("Error code: {0}", e.SpeechErrorCode.ToString());
            this.WriteLine("Error text: {0}", e.SpeechErrorText);
            this.WriteLine();
        }

        #endregion EventHandler

        #region PrivateMethods

        private void UpdateUIControls()
        {
            this.ShortPhraseStartButton.IsEnabled = false;
            this.LongDictationStartButton.IsEnabled = false;
            this.PartialContentBox.Text = string.Empty;
            this.ContentBox.Text = string.Empty;
        }

        /// <summary>
        /// 创建语音识别的客户端类型的实例。
        /// 该实例可以识别来自文件和语音设备的语音。
        /// 语音数据会被切分很小的段，然后使用该实例连续的向服务端发送一段。
        /// </summary>
        /// <param name="mode"><see cref="SpeechRecognitionMode"/>指明语音识别的模式。</param>
        private void CreateDataRecoClient(SpeechRecognitionMode mode)
        {
            if (this.dataClient != null)
            {
                this.dataClient.Dispose();
                this.dataClient = null;
            }

            // 使用工厂类型的 CreateDataClient 方法创建 DataRecognitionClient 类型的实例。
            this.dataClient = SpeechRecognitionServiceFactory.CreateDataClient(
                mode,             // 指定语音识别的模式。 
                "en-US",          // 我们把语音中语言的类型 hardcode 为英语，因为我们的两个 demo 文件都是英语语音。
                SUBSCRIPTIONKEY); // Bing Speech API 服务实例的 key。

            // 为语音识别Event handlers for speech recognition results
            if (mode == SpeechRecognitionMode.ShortPhrase)
            {
                // 为 ShortPhrase 模式的识别结果添加处理程序。
                this.dataClient.OnResponseReceived += this.OnDataShortPhraseResponseReceivedHandler;
            }
            else
            {
                // 为 LongDictation 模式的识别结果添加处理程序。
                // 服务端根据分辨出的语句间的停顿会多次触发执行该处理程序。
                this.dataClient.OnResponseReceived += this.OnDataDictationResponseReceivedHandler;
            }

            // 在服务端执行语音识别的过程中，该处理程序会被执行多次，
            // 具体是在语音服务对语音的内容产生了预测的结果时，就会触发执行该处理程序。
            this.dataClient.OnPartialResponseReceived += this.OnPartialResponseReceivedHandler;

            // 在服务端检测到错误时，触发执行该处理程序。
            this.dataClient.OnConversationError += this.OnConversationErrorHandler;
        }

        /// <summary>
        /// 向服务端发送语音数据。
        /// </summary>
        /// <param name="wavFileName">wav 格式文件的名称。</param>
        private void SendAudioHelper(string wavFileName)
        {
            using (FileStream fileStream = new FileStream(wavFileName, FileMode.Open, FileAccess.Read))
            {
                // Note for wave files, we can just send data from the file right to the server.
                // In the case you are not an audio file in wave format, and instead you have just
                // raw data (for example audio coming over bluetooth), then before sending up any 
                // audio data, you must first send up an SpeechAudioFormat descriptor to describe 
                // the layout and format of your raw audio data via DataRecognitionClient's sendAudioFormat() method.
                int bytesRead = 0;
                byte[] buffer = new byte[1024];

                try
                {
                    do
                    {
                        // 把文件数据读取到 buffer 中。
                        bytesRead = fileStream.Read(buffer, 0, buffer.Length);

                        // 通过 DataRecognitionClient 类型的实例把语音数据发送到服务端。 
                        this.dataClient.SendAudio(buffer, bytesRead);
                    }
                    while (bytesRead > 0);
                }
                finally
                {
                    // 告诉服务端语音数据已经传送完了。
                    this.dataClient.EndAudio();
                }
            }
        }

        /// <summary>
        /// 把服务端返回的语音识别结果输出到 UI。
        /// </summary>
        /// <param name="e"><see cref="SpeechResponseEventArgs"/>该类型的实例包含语音识别的结果。</param>
        private void WriteResponseResult(SpeechResponseEventArgs e)
        {
            if (e.PhraseResponse.Results.Length == 0)
            {
                this.WriteLine("No phrase response is available.");
            }
            else
            {
                this.WriteLine("********* Final n-BEST Results *********");
                for (int i = 0; i < e.PhraseResponse.Results.Length; i++)
                {
                    this.WriteLine(
                        "[{0}] Confidence={1}, Text=\"{2}\"",
                        i,
                        e.PhraseResponse.Results[i].Confidence,
                        e.PhraseResponse.Results[i].DisplayText);
                }
                this.WriteLine();
            }
        }

        /// <summary>
        /// 把服务端返回的语音识别结果输出到 UI。
        /// </summary>
        private void WriteLine()
        {
            this.WriteLine(string.Empty);
        }

        /// <summary>
        /// 把服务端返回的语音识别结果输出到 UI。
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The arguments.</param>
        private void WriteLine(string format, params object[] args)
        {
            var formattedStr = string.Format(format, args);
            Dispatcher.Invoke(() =>
            {
                ContentBox.Text += (formattedStr + "\n");
                ContentBox.ScrollToEnd();
            });
        }

        /// <summary>
        /// 把服务端返回的部分语音识别结果输出到 UI。
        /// </summary>
        private void PartialResponseWriteLine()
        {
            this.PartialResponseWriteLine(string.Empty);
        }

        /// <summary>
        /// 把服务端返回的部分结果输出到 UI。
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        private void PartialResponseWriteLine(string format, params object[] args)
        {
            var formattedStr = string.Format(format, args);
            Dispatcher.Invoke(() =>
            {
                PartialContentBox.Text += (formattedStr + "\n");
                PartialContentBox.ScrollToEnd();
            });
        }

        #endregion PrivateMethods
    }
}
