

namespace voice2text
{
    class Program
    {
        static void Main2(string[] args)
        {
            System.Net.WebClient wc = new System.Net.WebClient();
            try
            {
                wc.DownloadFile("https://ks3-cn-beijing.ksyun.com/zhaixinrui/test.txt", @"C:\1234lhw.dat");
                System.Console.WriteLine("下载成功: C:\\1234lhw.dat");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine("文件下载失败:"+ex.Message);
                System.Console.WriteLine(ex.StackTrace);
            }
            System.Console.Write("请按任意键退出...");
            System.Console.ReadKey(true);
           
            
        }
        static void Main(string[] args) {
        }
    }
}
