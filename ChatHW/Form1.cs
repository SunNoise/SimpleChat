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
    public partial class Form1 : Form
    {
        private Socket g_server_conn;
        public static List<string> connections;

        public Form1()
        {
            InitializeComponent();

            connections = new List<string>();
            //start listening (server part)
            int port = 2015;
            TRYANOTHERPORT:
            try
            {
                Listen(port);
            }
            catch (Exception ex)
            {
                port++;
                goto TRYANOTHERPORT;
            }
            this.Text = port.ToString();
        }

        private void Listen(int port)
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

        private void button1_Click(object sender, EventArgs e)
        {
            IPEndPoint remote_ep;
            var connectTo = textBox1.Text.Split(':');
            if(connectTo.Length == 2)
                remote_ep = new IPEndPoint(IPAddress.Parse(connectTo[0]), int.Parse(connectTo[1]));
            else
                remote_ep = new IPEndPoint(IPAddress.Parse(connectTo[0]), 2015);

            if (connections.Contains(remote_ep.ToString())) return;
            RunInstance(null, "Client", remote_ep);
            connections.Add(remote_ep.ToString());
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

    }
}
