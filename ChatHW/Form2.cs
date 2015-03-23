using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ChatHW
{
    public partial class Form2 : Form
    {
        private Socket g_conn;
        private byte[] g_bmsg = new byte[Message.SIZE];
        private DiffieHellman dh;
        private Random rnd;
        private string whoAmI, ourPublicKey;

        public Form2(Socket conn, string title, IPEndPoint remote_ep = null)
        {
            rnd = new Random();
            InitializeComponent();
            this.Text = title;
            whoAmI = title;
            if (remote_ep != null)
            {
                g_conn = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                g_conn.BeginConnect(remote_ep, new AsyncCallback(Connect), g_conn);
            }
            else
            {
                g_conn = conn;
                g_conn.BeginReceive(g_bmsg, 0, g_bmsg.Length, SocketFlags.None, new AsyncCallback(Receive), g_conn);
            }
        }

        private void Connect(IAsyncResult ar)
        {
            Socket client_conn = (Socket)ar.AsyncState;
            
            try
            {
                g_conn.BeginReceive(g_bmsg, 0, g_bmsg.Length, SocketFlags.None, new AsyncCallback(Receive), g_conn);
                dh = new DiffieHellman(rnd, out ourPublicKey);
                SendKey(Function.SIMP_INIT_COMM);
            }
            catch (Exception)
            {
                MessageBox.Show(@"No se pudo conectar");
                Environment.Exit(1);
            }
        }

        private int bytesInBuffer = 0;
        private byte[] buffer = new byte[Message.SIZE];
        private int receivedCount = 0;
        private void Receive(IAsyncResult ar)
        {
            Socket conn = (Socket)ar.AsyncState;
            
            int readBytes;
            try
            {
                readBytes = conn.EndReceive(ar);
            }
            catch (Exception) //connection has been lost
            {
                if (this.IsHandleCreated)
                    this.Invoke((MethodInvoker)delegate
                    {
                        this.Close();
                    });
                return;
            }
            int realReadBytes = readBytes;
            if (readBytes == 0)
            {
                Thread.Sleep(50);
                if (this.IsHandleCreated)
                    this.Invoke((MethodInvoker)delegate
                    {
                        this.Close();
                    });
                return;
            }
            if (bytesInBuffer + readBytes > 255)
                readBytes = 256 - bytesInBuffer;
            System.Buffer.BlockCopy(g_bmsg, 0, buffer, bytesInBuffer, readBytes);

            bytesInBuffer += readBytes;
            if (bytesInBuffer < 256)
            {
                g_bmsg = new byte[Message.SIZE];
                g_conn.BeginReceive(g_bmsg, 0, g_bmsg.Length, SocketFlags.None, new AsyncCallback(Receive), g_conn);
                return;
            }
            bytesInBuffer = 0;

            var encBuffer = new Message(buffer);
            Message message;
            if (receivedCount > 0)
            {
                var decBuffer = DES.Decrypt(encBuffer.CompleteBytes, Encoding.GetEncoding(28591).GetBytes(dh.key));
                message = new Message(decBuffer);
            }
            else
            {
                message = encBuffer;
            }
            var msg = Encoding.GetEncoding(28591).GetString(message.getData);
            msg = msg.Replace("\0", "");
            if (receivedCount == 0)
            {
                receivedCount++;
                var func = BitConverter.ToChar(message.getFunction, 0);
                if (func == (char) Function.SIMP_INIT_COMM)
                {
                    CalculateDH(msg);
                    SendKey(Function.SIMP_KEY_COMPUTED);
                    g_conn.BeginReceive(g_bmsg, 0, g_bmsg.Length, SocketFlags.None, new AsyncCallback(Receive), g_conn);
                    return;
                }
                else if (func == (char) Function.SIMP_KEY_COMPUTED)
                {
                    UpdateDH(msg);
                    g_conn.BeginReceive(g_bmsg, 0, g_bmsg.Length, SocketFlags.None, new AsyncCallback(Receive), g_conn);
                    return;
                }
            }
            if (!String.IsNullOrEmpty(msg))
                PublishMessage(listBox1, msg);

            buffer = new byte[Message.SIZE];
            if (realReadBytes != readBytes)
            {
                int offset = realReadBytes - readBytes;
                System.Buffer.BlockCopy(g_bmsg, readBytes, buffer, 0, offset);
                bytesInBuffer = offset;
            }
            g_bmsg = new byte[Message.SIZE];
            g_conn.BeginReceive(g_bmsg, 0, g_bmsg.Length, SocketFlags.None, new AsyncCallback(Receive), g_conn);
            receivedCount++;
        }

        private void CalculateDH(string msg)
        {
            var splitMsg = msg.Split(',');
            if (splitMsg.Length == 3)
            {
                long q, a, y = 0;
                foreach (var piece in splitMsg)
                {
                    var split = piece.Split('=');
                    var variable = split[0];
                    switch (variable)
                    {
                        case "q":
                            q = long.Parse(split[1]);
                            break;
                        case "a":
                            a = long.Parse(split[1]);
                            break;
                        case "y":
                            y = long.Parse(split[1]);
                            break;
                    }
                }
                dh = new DiffieHellman(rnd, y, out ourPublicKey);
                PublishMessage(listBox1, String.Format("Calculated key: {0}", dh.key));
            }
            else
            {
                throw new FormatException();
            }
        }

        private void UpdateDH(string msg)
        {
            var splitMsg = msg.Split(',');
            if (splitMsg.Length == 3)
            {
                long q, a, y = 0;
                foreach (var piece in splitMsg)
                {
                    var split = piece.Split('=');
                    var variable = split[0];
                    switch (variable)
                    {
                        case "q":
                            q = long.Parse(split[1]);
                            break;
                        case "a":
                            a = long.Parse(split[1]);
                            break;
                        case "y":
                            y = long.Parse(split[1]);
                            break;
                    }
                }
                dh.Update(y);
                PublishMessage(listBox1, String.Format("Calculated key: {0}", dh.key));
            }
            else
            {
                throw new FormatException();
            }
        }

        private void Send(string text)
        {
            var message = new Message(text);
            var encBuffer = DES.Encrypt(message.CompleteBytes, Encoding.GetEncoding(28591).GetBytes(dh.key));

            g_conn.Send(encBuffer, 0, Message.SIZE, SocketFlags.None);
            PublishMessage(listBox1, text);
        }

        private void SendKey(Function func)
        {
            var text = String.Format("q={2},a={1},y={0}", ourPublicKey, dh.a, dh.q);
            var message = new Message(text, func);

            g_conn.Send(message.CompleteBytes, 0, Message.SIZE, SocketFlags.None);
            PublishMessage(listBox1, "Key sent.");
        }

        private void PublishMessage(ListBox listBox, string mes)
        {
            if (InvokeRequired)
            {
                BeginInvoke((ThreadStart)delegate { PublishMessage(listBox, mes); });
                return;
            }

            listBox.Items.Add(mes);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Send(textBox1.Text);
            textBox1.Text = "";
        }

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            g_conn.Disconnect(false);
            Form1.connections.Remove(g_conn.RemoteEndPoint.ToString());
        }
    }
}
