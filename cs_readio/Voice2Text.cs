using Baidu.Aip.Speech;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cs_readio
{
    class Voice2Text
    {
        static string APP_ID = "9535757";
        static string API_KEY = "bL3SfB9bd2k6oeUmRDBBUZm5";
        static string SECRET_KEY = "9ed3ce5ed94bc37c982e79a5e04020a6";


        static Asr client = new Asr(API_KEY, SECRET_KEY);


        public static string GetText(string filename) {
            if (File.Exists(filename) == false)
            {
                return null;
            }

            var data = File.ReadAllBytes(filename);
            client.Timeout = 120000; // 若语音较长，建议设置更大的超时时间. ms
            Dictionary<string, object> dic = new Dictionary<string, object>();
            dic.Add("dev_pid", 1737);
            var result = client.Recognize(data, "wav", 16000, dic);
            return result["result"][0].ToString();
        }



    }
}
