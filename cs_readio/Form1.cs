using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Windows.Threading;

namespace cs_readio
{
    public partial class Form1 : Form
    {
        Radio radio;


        public Form1()
        {
            InitializeComponent();
        }

        private async void button_start_Click(object sender, EventArgs e)
        {
            string url = textBox_url.Text.Trim();
            await PlaySource(url);
        }
        private async void button_stop_Click(object sender, EventArgs e)
        {
            await StopPlaying();
        }


        private async Task PlaySource(string url)
        {
            //stop playing any current radio
            await StopPlaying();
            radio = new Radio(url);

            radio.Start();
        }

        private async Task StopPlaying()
        {
            if (radio!=null)
                await Task.Run(() => radio.Stop());
            radio = null;
        }


    }
}
