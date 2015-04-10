using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Utils.Dlg;

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

        private byte[] fileTransBuffer;
        private int bytesInBuffer = 0, bytesInFileBuffer = 0;
        private byte[] buffer = new byte[Message.SIZE];
        private int receivedCount = 0, receivedFileCount = 0;
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
            var msg = message.getData;
            var func = BitConverter.ToChar(message.getFunction, 0);
            string msgString = "";
            if (receivedCount == 0)
            {
                msgString = Encoding.GetEncoding(28591).GetString(msg);
                msgString = msgString.Replace("\0", "");
                receivedCount++;
                if (func == (char) Function.SIMP_INIT_COMM)
                {
                    CalculateDH(msgString);
                    SendKey(Function.SIMP_KEY_COMPUTED);
                    g_conn.BeginReceive(g_bmsg, 0, g_bmsg.Length, SocketFlags.None, new AsyncCallback(Receive), g_conn);
                    return;
                }
                if (func == (char) Function.SIMP_KEY_COMPUTED)
                {
                    UpdateDH(msgString);
                    g_conn.BeginReceive(g_bmsg, 0, g_bmsg.Length, SocketFlags.None, new AsyncCallback(Receive), g_conn);
                    return;
                }
                throw new Exception("Rare Error Found!");
            }
            switch (func)
            {
                case (char)Function.SIMP_CHAT_MSG:
                    msgString = Encoding.GetEncoding(28591).GetString(msg);
                    msgString = msgString.Replace("\0", "");
                    if (!String.IsNullOrEmpty(msgString))
                    PublishMessage(listBox1, msgString);
                    break;
                case (char)Function.SIMP_CHAT_FILEINIT:
                    msgString = Encoding.GetEncoding(28591).GetString(msg);
                    msgString = msgString.Replace("\0", "");
                    var split = msgString.Split(',');
                    var filename = split[0];
                    PublishMessage(listBox1, "Peer is trying to send: "+ filename);
                    var size = int.Parse(split[1]);
                    var filenameSplit = filename.Split('.');
                    string filext = "";
                    if(filenameSplit.Length > 1)
                        filext = filenameSplit.Last();
                    SaveFileDiag(filext, size);
                    break;
                case (char)Function.SIMP_CHAT_FILEINITANS:
                    msgString = Encoding.GetEncoding(28591).GetString(msg);
                    msgString = msgString.Replace("\0", "");
                    bool answer = bool.Parse(msgString);
                    if (answer)
                    {
                        PublishMessage(listBox1, "Transfer starting...");
                        SendFile();
                    }
                    else
                    {
                        PublishMessage(listBox1, "Transfer canceled by peer");
                        ModifyButton(btnSendFile, true);
                    }
                    break;
                case (char)Function.SIMP_CHAT_FILETRANS:
                    receivedFileCount++;
                    System.Buffer.BlockCopy(msg, 0, fileTransBuffer, bytesInFileBuffer, message.getSize);
                    bytesInFileBuffer += message.getSize;

                    var recMessage = new Message(receivedFileCount.ToString(), Function.SIMP_CHAT_FILETRANSREC);
                    var recEncrypted = DES.Encrypt(recMessage.CompleteBytes, Encoding.GetEncoding(28591).GetBytes(dh.key));
                    g_conn.Send(recEncrypted, 0, Message.SIZE, SocketFlags.None);
                    break;
                case (char)Function.SIMP_CHAT_FILETRANSREC:
                    msgString = Encoding.GetEncoding(28591).GetString(msg);
                    var peerReceivedCount = int.Parse(msgString);
                    receivedFilePart = true;
                    sentFileCount = peerReceivedCount;
                    break;
                case (char)Function.SIMP_CHAT_FILETRANSNOTREC:
                    msgString = Encoding.GetEncoding(28591).GetString(msg);
                    var peerCurrentIteration = int.Parse(msgString);
                    var notRecMessage = new Message(receivedFileCount.ToString(), Function.SIMP_CHAT_FILETRANSREC);
                    var notRecEncrypted = DES.Encrypt(notRecMessage.CompleteBytes, Encoding.GetEncoding(28591).GetBytes(dh.key));
                    g_conn.Send(notRecEncrypted, 0, Message.SIZE, SocketFlags.None);
                    break;
                case (char)Function.SIMP_CHAT_FILETRANSEND:
                    if (fileTransBuffer != null)
                    {
                        File.WriteAllBytes(saveFilePath, fileTransBuffer);
                        var endMessage = new Message("", Function.SIMP_CHAT_FILETRANSEND);
                        var endEncrypted = DES.Encrypt(endMessage.CompleteBytes, Encoding.GetEncoding(28591).GetBytes(dh.key));
                        g_conn.Send(endEncrypted, 0, Message.SIZE, SocketFlags.None);
                    }
                    receivedFileCount = 0;
                    bytesInFileBuffer = 0;
                    sendingFileBytes = null;
                    fileTransBuffer = null;
                    saveFilePath = null;
                    PublishMessage(listBox1, "File Transfer Complete");
                    ModifyButton(btnSendFile, true);
                    break;
            }
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

        private void Send(string text, Function func = Function.SIMP_CHAT_MSG)
        {
            var message = new Message(text, func);
            var encBuffer = DES.Encrypt(message.CompleteBytes, Encoding.GetEncoding(28591).GetBytes(dh.key));

            g_conn.Send(encBuffer, 0, Message.SIZE, SocketFlags.None);

            if(func == Function.SIMP_CHAT_MSG)
                PublishMessage(listBox1, text);
        }

        private int sentFileCount;
        private bool receivedFilePart;
        private void SendFile()
        {
            (new Thread(() =>
            {
                double i = Math.Ceiling(sendingFileBytes.Length/236f);
                int currentOffset = 0;
                int remainder = sendingFileBytes.Length;
                receivedFilePart = true;
                for (sentFileCount = 0; sentFileCount < i;)
                {
                    int timeout = 0;
                    while (!receivedFilePart)
                    {
                        Thread.Sleep(5);
                        if (timeout > 500)
                        {
                            var tOMessage = new Message(sentFileCount.ToString(), Function.SIMP_CHAT_FILETRANSNOTREC);
                            var tOencBuffer = DES.Encrypt(tOMessage.CompleteBytes, Encoding.GetEncoding(28591).GetBytes(dh.key));
                            g_conn.Send(tOencBuffer, 0, Message.SIZE, SocketFlags.None);
                            timeout = 0;
                        }
                        timeout++;
                    }
                    if (sentFileCount >= i) break;
                    currentOffset = 236 * sentFileCount;
                    remainder = sendingFileBytes.Length - (236 * sentFileCount);
                    int sendNumber = remainder > 236 ? 236 : remainder;
                    byte[] sending = new byte[sendNumber];
                    System.Buffer.BlockCopy(sendingFileBytes, currentOffset, sending, 0, sendNumber);

                    var message = new Message(sending);
                    var encBuffer = DES.Encrypt(message.CompleteBytes, Encoding.GetEncoding(28591).GetBytes(dh.key));
                    g_conn.Send(encBuffer, 0, Message.SIZE, SocketFlags.None);
                    receivedFilePart = false;
                }
                var endMessage = new Message("", Function.SIMP_CHAT_FILETRANSEND);
                var endEncrypted = DES.Encrypt(endMessage.CompleteBytes, Encoding.GetEncoding(28591).GetBytes(dh.key));
                g_conn.Send(endEncrypted, 0, Message.SIZE, SocketFlags.None);
            })).Start();
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

        private void ModifyButton(Button btn, bool value)
        {
            if (InvokeRequired)
            {
                BeginInvoke((ThreadStart)delegate { ModifyButton(btn, value); });
                return;
            }

            btn.Enabled = value;
        }

        private string saveFilePath;
        private void SaveFileDiag(string filext, int size)
        {
            ModifyButton(btnSendFile, false);
            (new Thread(() =>
            {
                var dlg = new CFileSaveDlgThreadApartmentSafe();
                if (!String.IsNullOrEmpty(filext))
                {
                    dlg = new CFileSaveDlgThreadApartmentSafe
                    {
                        Filter = filext + " file|*." + filext,
                        DefaultExt = filext
                    };
                }

                Point ptStartLocation = new Point(this.Location.X, this.Location.Y);

                dlg.StartupLocation = ptStartLocation;

                DialogResult result = dlg.ShowDialog();
                if (result == DialogResult.OK) // Test result.
                {
                    saveFilePath = dlg.FilePath;
                    try
                    {
                        fileTransBuffer = new byte[size];
                        PublishMessage(listBox1, "Receiving file...");
                        Send("true", Function.SIMP_CHAT_FILEINITANS);
                    }
                    catch (IOException)
                    {
                        MessageBox.Show("There was an error.");
                        PublishMessage(listBox1, "Transfer canceled");
                        Send("false", Function.SIMP_CHAT_FILEINITANS);
                        ModifyButton(btnSendFile, true);
                    }
                }
                else
                {
                    PublishMessage(listBox1, "Transfer canceled");
                    Send("false", Function.SIMP_CHAT_FILEINITANS);
                    ModifyButton(btnSendFile, true);
                }
            })).Start();
        }

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            g_conn.Disconnect(false);
            Form1.connections.Remove(g_conn.RemoteEndPoint.ToString());
        }

        private void btnSendMsg_Click(object sender, EventArgs e)
        {
            Send(textBox1.Text);
            textBox1.Text = "";
        }

        private byte[] sendingFileBytes;
        private void btnSendFile_Click(object sender, EventArgs e)
        {
            ModifyButton(btnSendFile, false);
            (new Thread(() =>
            {
                int size = -1;
                CFileOpenDlgThreadApartmentSafe dlg = new CFileOpenDlgThreadApartmentSafe();

                Point ptStartLocation = new Point(this.Location.X, this.Location.Y);

                dlg.StartupLocation = ptStartLocation;

                DialogResult result = dlg.ShowDialog();
                
                if (result == DialogResult.OK) // Test result.
                {
                    string filePath = dlg.FilePath;
                    string fileName = filePath.Split('\\').Last();
                    string message = "";
                    try
                    {
                        sendingFileBytes = File.ReadAllBytes(filePath);
                        size = sendingFileBytes.Length;
                        message = String.Format("{0},{1}", fileName, size);
                    }
                    catch (IOException)
                    {
                        MessageBox.Show("There was an error.");
                        ModifyButton(btnSendFile, true);
                        return;
                    }
                    PublishMessage(listBox1, "Waiting for answer...");
                    Send(message, Function.SIMP_CHAT_FILEINIT);
                }
                else
                {
                    ModifyButton(btnSendFile, true);
                }
            })).Start();
        }
    }
}
