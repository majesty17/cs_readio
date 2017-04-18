using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Runtime.InteropServices;

namespace cs_readio
{
    public partial class Form1 : Form
    {

        [DllImport("winmm.dll", EntryPoint = "mciSendString", CharSet = CharSet.Auto)]
        public static extern int mciSendString(
         string lpstrCommand,
         string lpstrReturnString,
         int uReturnLength,
         int hwndCallback
        );  


        public Form1()
        {
            InitializeComponent();
        }




        //打开
        private void button_open_Click(object sender, EventArgs e)
        {
            string url = textBox_url.Text.Trim();
            axWindowsMediaPlayer1.URL = url;
            axWindowsMediaPlayer1.Ctlcontrols.play();
            
        }


        //测试
        private void button1_Click(object sender, EventArgs e)
        {

			//用来post
			HttpWebRequest request;
			//初始化httpwebrequest对象

            String post_url = "http://vop.baidu.com/server_api?lan=en&cuid=iamreadio_test&token=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
			request=(HttpWebRequest)WebRequest.Create(post_url);
			request.Method="POST";
            request.ServicePoint.Expect100Continue = false;
			request.ContentType="audio/wav;rate=8000";
			request.UserAgent="Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/39.0.2171.65 Safari/537.36";
			

			//填写post数据

            FileStream fsRead = new FileStream(@"C:\Users\Majesty\Desktop\8000.wav",FileMode.Open);
            byte[] data=new byte[102400];
            int data_len = fsRead.Read(data, 0, data.Length);
            fsRead.Close();

			request.ContentLength=data_len;
			request.GetRequestStream().Write(data,0,data_len);
				
			HttpWebResponse res=(HttpWebResponse)request.GetResponse();


			Stream st=res.GetResponseStream();
			Encoding en=Encoding.GetEncoding("utf-8");
			StreamReader st_r=new StreamReader(st,en);


			char[] read = new char[512];
			// Reads 256 characters at a time.
			int count = st_r.Read( read, 0, 512 );
			StringBuilder sb=new StringBuilder();
			while ( count > 0 ){
				// Dumps the 256 characters on a String* and displays the String* to the console.
				String str = new String( read,0,count );
				sb.Append(str);
				count = st_r.Read( read, 0, 512);
			}
		

            richTextBox1.AppendText(sb.ToString());
			
                        
		
        }
    }
}
