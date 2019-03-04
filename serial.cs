1.	using System;  
2.	using System.Collections.Generic;  
3.	using System.ComponentModel;  
4.	using System.Data;  
5.	using System.Drawing;  
6.	using System.Linq;  
7.	using System.Text;  
8.	using System.Windows.Forms;  
9.	using System.IO.Ports;  
10.	using System.Text.RegularExpressions;  
11.	namespace SerialportSample  
12.	{  
13.	    public partial class SerialportSampleForm : Form  
14.	    {  
15.	        private SerialPort comm = new SerialPort();  
16.	        private StringBuilder builder = new StringBuilder();//避免在事件处理方法中反复的创建，定义到外面。   
17.	        private long received_count = 0;//接收计数   
18.	        private long send_count = 0;//发送计数   
19.	        private bool Listening = false;//是否没有执行完invoke相关操作   
20.	        private bool Closing = false;//是否正在关闭串口，执行Application.DoEvents，并阻止再次invoke   
21.	        public SerialportSampleForm()  
22.	        {  
23.	            InitializeComponent();  
24.	        }  
25.	        //窗体初始化   
26.	        private void Form1_Load(object sender, EventArgs e)  
27.	        {  
28.	            //初始化下拉串口名称列表框   
29.	            string[] ports = SerialPort.GetPortNames();  
30.	            Array.Sort(ports);  
31.	            comboPortName.Items.AddRange(ports);  
32.	            comboPortName.SelectedIndex = comboPortName.Items.Count > 0 ? 0 : -1;  
33.	            comboBaudrate.SelectedIndex = comboBaudrate.Items.IndexOf("9600");  
34.	            //初始化SerialPort对象   
35.	            comm.NewLine = "/r/n";  
36.	            comm.RtsEnable = true;//根据实际情况吧。   
37.	            //添加事件注册   
38.	            comm.DataReceived += comm_DataReceived;  
39.	        }  
40.	        void comm_DataReceived(object sender, SerialDataReceivedEventArgs e)  
41.	        {  
42.	            if (Closing) return;//如果正在关闭，忽略操作，直接返回，尽快的完成串口监听线程的一次循环   
43.	            try  
44.	            {  
45.	                Listening = true;//设置标记，说明我已经开始处理数据，一会儿要使用系统UI的。   
46.	                int n = comm.BytesToRead;//先记录下来，避免某种原因，人为的原因，操作几次之间时间长，缓存不一致   
47.	                byte[] buf = new byte[n];//声明一个临时数组存储当前来的串口数据   
48.	                received_count += n;//增加接收计数   
49.	                comm.Read(buf, 0, n);//读取缓冲数据   
50.	                builder.Clear();//清除字符串构造器的内容   
51.	                //因为要访问ui资源，所以需要使用invoke方式同步ui。   
52.	                this.Invoke((EventHandler)(delegate  
53.	                {  
54.	                    //判断是否是显示为16禁止   
55.	                    if (checkBoxHexView.Checked)  
56.	                    {  
57.	                        //依次的拼接出16进制字符串   
58.	                        foreach (byte b in buf)  
59.	                        {  
60.	                            builder.Append(b.ToString("X2") + " ");  
61.	                        }  
62.	                    }  
63.	                    else  
64.	                    {  
65.	                        //直接按ASCII规则转换成字符串   
66.	                        builder.Append(Encoding.ASCII.GetString(buf));  
67.	                    }  
68.	                    //追加的形式添加到文本框末端，并滚动到最后。   
69.	                    this.txGet.AppendText(builder.ToString());  
70.	                    //修改接收计数   
71.	                    labelGetCount.Text = "Get:" + received_count.ToString();  
72.	                }));  
73.	            }  
74.	            finally  
75.	            {  
76.	                Listening = false;//我用完了，ui可以关闭串口了。   
77.	            }  
78.	        }  
79.	        private void buttonOpenClose_Click(object sender, EventArgs e)  
80.	        {  
81.	            //根据当前串口对象，来判断操作   
82.	            if (comm.IsOpen)  
83.	            {  
84.	                Closing = true;  
85.	                while (Listening) Application.DoEvents();  
86.	                //打开时点击，则关闭串口   
87.	                comm.Close();  
88.	                Closing = false;  
89.	            }  
90.	            else  
91.	            {  
92.	                //关闭时点击，则设置好端口，波特率后打开   
93.	                comm.PortName = comboPortName.Text;  
94.	                comm.BaudRate = int.Parse(comboBaudrate.Text);  
95.	                try  
96.	                {  
97.	                    comm.Open();  
98.	                }  
99.	                catch(Exception ex)  
100.	                {  
101.	                    //捕获到异常信息，创建一个新的comm对象，之前的不能用了。   
102.	                    comm = new SerialPort();  
103.	                    //现实异常信息给客户。   
104.	                    MessageBox.Show(ex.Message);  
105.	                }  
106.	            }  
107.	            //设置按钮的状态   
108.	            buttonOpenClose.Text = comm.IsOpen ? "Close" : "Open";  
109.	            buttonSend.Enabled = comm.IsOpen;  
110.	        }  
111.	        //动态的修改获取文本框是否支持自动换行。   
112.	        private void checkBoxNewlineGet_CheckedChanged(object sender, EventArgs e)  
113.	        {  
114.	            txGet.WordWrap = checkBoxNewlineGet.Checked;  
115.	        }  
116.	        private void buttonSend_Click(object sender, EventArgs e)  
117.	        {  
118.	            //定义一个变量，记录发送了几个字节   
119.	            int n = 0;  
120.	            //16进制发送   
121.	            if (checkBoxHexSend.Checked)  
122.	            {  
123.	                //我们不管规则了。如果写错了一些，我们允许的，只用正则得到有效的十六进制数   
124.	                MatchCollection mc = Regex.Matches(txSend.Text, @"(?i)[/da-f]{2}");  
125.	                List<byte> buf = new List<byte>();//填充到这个临时列表中   
126.	                //依次添加到列表中   
127.	                foreach (Match m in mc)  
128.	                {  
129.	                    buf.Add(byte.Parse(m.Value));  
130.	                }  
131.	                //转换列表为数组后发送   
132.	                comm.Write(buf.ToArray(), 0, buf.Count);  
133.	                //记录发送的字节数   
134.	                n = buf.Count;  
135.	            }  
136.	            else//ascii编码直接发送   
137.	            {  
138.	                //包含换行符   
139.	                if (checkBoxNewlineSend.Checked)  
140.	                {  
141.	                    comm.WriteLine(txSend.Text);  
142.	                    n = txSend.Text.Length + 2;  
143.	                }  
144.	                else//不包含换行符   
145.	                {  
146.	                    comm.Write(txSend.Text);  
147.	                    n = txSend.Text.Length;  
148.	                }  
149.	            }  
150.	            send_count += n;//累加发送字节数   
151.	            labelSendCount.Text = "Send:" + send_count.ToString();//更新界面   
152.	        }  
153.	        private void buttonReset_Click(object sender, EventArgs e)  
154.	        {  
155.	            //复位接受和发送的字节数计数器并更新界面。   
156.	            send_count = received_count = 0;  
157.	            labelGetCount.Text = "Get:0";  
158.	            labelSendCount.Text = "Send:0";  
159.	        }  
160.	    }  
161.	}  
