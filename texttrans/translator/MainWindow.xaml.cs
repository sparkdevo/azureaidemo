using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Runtime.Serialization;
using System.Windows.Media.Imaging;

namespace translator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Translator text subscription key from Microsoft Azure dashboard
        const string TEXT_TRANSLATION_API_SUBSCRIPTION_KEY = "757562e0fcd147eea4602aa8cbfa506a";
        const string BING_SPELL_CHECK_API_SUBSCRIPTION_KEY = "d3d0b46a3bab47fb95e0cbcaacc52e80";

        const string TEXT_TRANSLATION_API_ENDPOINT = "https://api.microsofttranslator.com/v2/Http.svc/";
        const string BING_SPELL_CHECK_API_ENDPOINT = "https://api.cognitive.microsoft.com/bing/v7.0/spellcheck/";

        private string[] languageCodes;     // array of language codes

        // Dictionary to map language code from friendly name (sorted case-insensitively on language name)
        private SortedDictionary<string, string> languageCodesAndTitles =
            new SortedDictionary<string, string>(Comparer<string>.Create((a, b) => string.Compare(a, b, true)));

        public MainWindow()
        {
            // at least show an error dialog when we get an unexpected error
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(HandleExceptions);

            if (TEXT_TRANSLATION_API_SUBSCRIPTION_KEY.Length != 32
                || BING_SPELL_CHECK_API_SUBSCRIPTION_KEY.Length != 32)
            {
                MessageBox.Show("One or more invalid API subscription keys.\n\n" +
                    "Put your keys in the *_API_SUBSCRIPTION_KEY variables in MainWindow.xaml.cs.",
                    "Invalid Subscription Key(s)", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
            }
            else
            {
                InitializeComponent();          // start the GUI
                BitmapImage img = new BitmapImage();
                img.BeginInit();
                img.StreamSource = this.GetType().Assembly.GetManifestResourceStream("translator.Resources.MSTIcon.ico");
                img.EndInit();
                this.Icon = img;
                GetLanguagesForTranslate();     // get codes of languages that can be translated
                GetLanguageNames();             // get friendly names of languages
                PopulateLanguageMenus();        // fill the drop-down language lists
            }
        }

        // Global exception handler to display error message and exit
        private static void HandleExceptions(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            MessageBox.Show("Caught " + e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            System.Windows.Application.Current.Shutdown();
        }

        // ***** POPULATE LANGUAGE MENUS
        private void PopulateLanguageMenus()
        {
            int count = languageCodesAndTitles.Count;
            foreach (string menuItem in languageCodesAndTitles.Keys)
            {
                FromLanguageComboBox.Items.Add(menuItem);
                ToLanguageComboBox.Items.Add(menuItem);
            }

            // 设置默认的源语言和目标语言
            FromLanguageComboBox.SelectedItem = "英语";
            ToLanguageComboBox.SelectedItem = "简体中文";
        }

        // ***** CORRECT SPELLING OF TEXT TO BE TRANSLATED
        private string CorrectSpelling(string text)
        {
            string uri = BING_SPELL_CHECK_API_ENDPOINT + "?mode=spell&mkt=en-US";
            // 创建拼写检查的请求
            HttpWebRequest spellCheckWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            spellCheckWebRequest.Headers.Add("Ocp-Apim-Subscription-Key", BING_SPELL_CHECK_API_SUBSCRIPTION_KEY);
            spellCheckWebRequest.Method = "POST";
            spellCheckWebRequest.ContentType = "application/x-www-form-urlencoded"; // 这个设置是必须的！

            // 把文本内容放在请求的 body 中
            string body = "text=" + System.Web.HttpUtility.UrlEncode(text);
            byte[] data = Encoding.UTF8.GetBytes(body);
            spellCheckWebRequest.ContentLength = data.Length;
            using (var requestStream = spellCheckWebRequest.GetRequestStream())
                requestStream.Write(data, 0, data.Length);
            HttpWebResponse response = (HttpWebResponse)spellCheckWebRequest.GetResponse();

            // 从返回中取出 json 格式的拼写检查结果
            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            var responseStream = response.GetResponseStream();
            var jsonString = new StreamReader(responseStream, Encoding.GetEncoding("utf-8")).ReadToEnd();
            dynamic jsonResponse = serializer.DeserializeObject(jsonString);
            var flaggedTokens = jsonResponse["flaggedTokens"];

            // 我们定义一个规则来应用拼写检查的结果，
            // 比如：当 拼写检查的权值大于 0.7 时就用建议的值替换掉文本中的值。
            var corrections = new SortedDictionary<int, string[]>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
            for (int i = 0; i < flaggedTokens.Length; i++)
            {
                var correction = flaggedTokens[i];
                var suggestion = correction["suggestions"][0];
                if (suggestion["score"] > (decimal)0.7)         
                    corrections[(int)correction["offset"]] = new string[]   
                        { correction["token"], suggestion["suggestion"] }; 
            }

            foreach (int i in corrections.Keys)
            {
                var oldtext = corrections[i][0];
                var newtext = corrections[i][1];
                if (text.Substring(i, oldtext.Length).All(char.IsUpper)) newtext = newtext.ToUpper();
                else if (char.IsUpper(text[i])) newtext = newtext[0].ToString().ToUpper() + newtext.Substring(1);
                text = text.Substring(0, i) + newtext + text.Substring(i + oldtext.Length);
            }
            return text;
        }

        // ***** GET TRANSLATABLE LANGAUGE CODES
        private void GetLanguagesForTranslate()
        {
            // 获得翻译服务支持的语言
            string uri = TEXT_TRANSLATION_API_ENDPOINT + "GetLanguagesForTranslate?scope=text";
            WebRequest WebRequest = WebRequest.Create(uri);
            WebRequest.Headers.Add("Ocp-Apim-Subscription-Key", TEXT_TRANSLATION_API_SUBSCRIPTION_KEY);
            WebResponse response = null;

            // 把返回的 xml 信息抽取到数组中
            response = WebRequest.GetResponse();
            using (Stream stream = response.GetResponseStream())
            {
                DataContractSerializer dcs = new DataContractSerializer(typeof(List<string>));
                List<string> languagesForTranslate = (List<string>)dcs.ReadObject(stream);
                languageCodes = languagesForTranslate.ToArray();
            }
        }

        //***** GET FRIENDLY LANGUAGE NAMES
        private void GetLanguageNames()
        {
            // 获得简体中文的语言名称
            string uri = TEXT_TRANSLATION_API_ENDPOINT + "GetLanguageNames?locale=zh-CHS";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Headers.Add("Ocp-Apim-Subscription-Key", TEXT_TRANSLATION_API_SUBSCRIPTION_KEY);
            request.ContentType = "text/xml";
            request.Method = "POST";
            DataContractSerializer dcs = new DataContractSerializer(Type.GetType("System.String[]"));
            using (Stream stream = request.GetRequestStream())
            {
                dcs.WriteObject(stream, languageCodes);
            }
                
            // 把返回的 xml 信息抽取到数组中
            var response = request.GetResponse();
            string[] languageNames;
            using (Stream stream = response.GetResponseStream())
            {
                languageNames = (string[])dcs.ReadObject(stream);
            }
                
            // 把支持的语言列表及其友好名称保存到字典数据结构中，
            // 随后会把它们绑定给 combo box 控件进行显示
            for (int i = 0; i < languageNames.Length; i++)
            {
                languageCodesAndTitles.Add(languageNames[i], languageCodes[i]);
            } 
        }

        // ***** PERFORM TRANSLATION ON BUTTON CLICK
        private void TranslateButton_Click(object sender, EventArgs e)
        {
            string textToTranslate = TextToTranslate.Text.Trim();
            string fromLanguage = FromLanguageComboBox.SelectedValue.ToString();
            string fromLanguageCode = languageCodesAndTitles[fromLanguage];
            string toLanguageCode = languageCodesAndTitles[ToLanguageComboBox.SelectedValue.ToString()];

            // 如果要翻译的文本是英语，还可以进行拼写检查
            if (fromLanguageCode == "en")
            {
                textToTranslate = CorrectSpelling(textToTranslate);
                // 把更新后的文本保存到 UI 控件上
                TextToTranslate.Text = textToTranslate;     
            }

            // 处理文本为空和不需要翻译的情况
            if (textToTranslate == "" || fromLanguageCode == toLanguageCode)
            {
                TranslatedText.Text = textToTranslate;
                return;
            }

            // 通过 http 请求执行翻译任务
            string uri = string.Format(TEXT_TRANSLATION_API_ENDPOINT + "Translate?text=" +
                System.Web.HttpUtility.UrlEncode(textToTranslate) + "&from={0}&to={1}", fromLanguageCode, toLanguageCode);
            var translationWebRequest = HttpWebRequest.Create(uri);
            translationWebRequest.Headers.Add("Ocp-Apim-Subscription-Key", TEXT_TRANSLATION_API_SUBSCRIPTION_KEY);
            WebResponse response = null;
            response = translationWebRequest.GetResponse();

            // 把返回的翻译结果抽取到 UI 控件中
            Stream stream = response.GetResponseStream();
            StreamReader translatedStream = new StreamReader(stream, Encoding.GetEncoding("utf-8"));
            System.Xml.XmlDocument xmlResponse = new System.Xml.XmlDocument();
            xmlResponse.LoadXml(translatedStream.ReadToEnd());
            TranslatedText.Text = xmlResponse.InnerText;
        }
    }
}
