using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ChatHW
{
    public partial class Form1 : Form
    {
        private const string SERVICE = "https://web111.secure-secure.co.uk/maryayi.com/messenger_service/request_service.php/request_service.php?";
        private const string PASS = "test";

        private Socket g_server_conn;
        public static List<string> connections;
        private Random rnd;
        private int port;

        public Form1()
        {
            rnd = new Random();
            InitializeComponent();

            connections = new List<string>();
            //start listening (server part)
            port = 2015;
            TRYANOTHERPORT:
            try
            {
                Listen();
            }
            catch (Exception ex)
            {
                port++;
                goto TRYANOTHERPORT;
            }
            this.Text = port.ToString();
        }

        private void Listen()
        {
            IPEndPoint local_ep = new IPEndPoint(IPAddress.Any, port);

            g_server_conn = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            g_server_conn.Bind(local_ep);

            g_server_conn.Listen(1);

            g_server_conn.BeginAccept(new AsyncCallback(Accept), null);
        }

        private void Accept(IAsyncResult ar)
        {
            Socket server_conn = g_server_conn.EndAccept(ar);

            if (connections.Contains(server_conn.RemoteEndPoint.ToString())) return;
            RunInstance(server_conn, "Server");
            connections.Add(server_conn.RemoteEndPoint.ToString());
            g_server_conn.BeginAccept(new AsyncCallback(Accept), null);
        }

        private void RunInstance(Socket conn, string text,IPEndPoint ep = null)
        {
            try{
                (new Thread(() =>
                {
                    Application.Run(new Form2(conn, text, ep));
                })).Start();
            }
            catch(Exception)
            {
                Console.WriteLine("Connection forcefully disconnected");
                this.Close(); // Dont shutdown because the socket may be disposed and its disconnected anyway
                if(ep == null)
                    connections.Remove(conn.RemoteEndPoint.ToString());
                else
                    connections.Remove(ep.ToString());
                return;
            }
        }

        private void btnLocate_Click(object sender, EventArgs e)
        {
            try
            {
                var user = txtConnect.Text;
                var nonce = rnd.Next();
                var hash = SHA1.Compute(Encoding.ASCII.GetBytes(PASS + nonce));
                var hashStr = string.Concat(hash.Select(x => x.ToString("x2")));
                var searchFor = txtLocate.Text;
                var connection = SERVICE + "service=LOCATE" + "&" +
                                 "user=" + user + "&" +
                                 "nonce=" + nonce + "&" +
                                 "hash=" + hashStr + "&" +
                                 "search_user=" + searchFor;
                WebRequest webRequest = WebRequest.Create(connection);
                WebResponse webResp = webRequest.GetResponse();

                string result = "";
                using (var reader = new StreamReader(webResp.GetResponseStream()))
                {
                    result = reader.ReadToEnd();
                    if (result.StartsWith("\nERROR"))
                        throw new Exception(result);
                }


                var connectTo = result.Split('/')[1].Split(':'); //fix this
                var remote_ep = new IPEndPoint(IPAddress.Parse(connectTo[0]), int.Parse(connectTo[1]));

                if (connections.Contains(remote_ep.ToString())) return;
                RunInstance(null, "Client", remote_ep);
                connections.Add(remote_ep.ToString());

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                var user = txtConnect.Text;
                var nonce = rnd.Next();
                var hash = SHA1.Compute(Encoding.ASCII.GetBytes(PASS + nonce));
                var hashStr = string.Concat(hash.Select(x => x.ToString("x2")));
                var localip = GetLocalIPv4(NetworkInterfaceType.Wireless80211);
                var connection = SERVICE + "service=CONNECT" + "&" +
                                 "user=" + user + "&" +
                                 "nonce=" + nonce + "&" +
                                 "hash=" + hashStr + "&" +
                                 "port=" + port + "&" +
                                 "private_ip=" + localip;
                WebRequest webRequest = WebRequest.Create(connection);
                WebResponse webResp = webRequest.GetResponse();

                using (var reader = new StreamReader(webResp.GetResponseStream()))
                {
                    string result = reader.ReadToEnd();
                    if (result.StartsWith("\nERROR"))
                        throw new Exception(result);
                }
                MessageBox.Show(@"Connection Successful");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public string GetLocalIPv4(NetworkInterfaceType _type)
        {
            string output = "";
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            output = ip.Address.ToString();
                        }
                    }
                }
            }
            return output;
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            try
            {
                var user = txtConnect.Text;
                var nonce = rnd.Next();
                var hash = SHA1.Compute(Encoding.ASCII.GetBytes(PASS + nonce));
                var hashStr = string.Concat(hash.Select(x => x.ToString("x2")));
                var connection = SERVICE + "service=DISCONNECT" + "&" +
                                 "user=" + user + "&" +
                                 "nonce=" + nonce + "&" +
                                 "hash=" + hashStr;
                WebRequest webRequest = WebRequest.Create(connection);
                WebResponse webResp = webRequest.GetResponse();

                using (var reader = new StreamReader(webResp.GetResponseStream()))
                {
                    string result = reader.ReadToEnd();
                    if (result.StartsWith("\nERROR"))
                        throw new Exception(result);
                }
                MessageBox.Show(@"Disconnected Successfully");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

    }
}
