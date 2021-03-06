﻿/*
 * Author: Gence Özer 
 * Date: 11/19/2016
 */
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
using System.Threading.Tasks;
using System.Windows.Forms;

namespace server
{
    public struct ClientConnection
    {
        public Socket socket;
        public Mutex mutex;
        public string username;
    }

    public partial class serverForm : Form
    {
        Socket socket;
        EndPoint epLocal;
        List<string> connectedUsers = new List<String>();
        Thread dispatcherThread;
        private static Mutex mut = new Mutex();
        private static Mutex textDbMut = new Mutex();
        private string pathDb;
        
        public serverForm()
        {
            InitializeComponent();
        }

        /*
         * This function prints the given message in the 
         * Logger rich text box in the GUI
         */
        private void printLogger(string message)
        {
            string now = DateTime.Now.ToString(@"MM\/dd\/yyyy h\:mm tt");
            RTextBox_Logs.AppendText(now + "--> " + message + "\n");
        }

        /*
         * This function returns local ip address of the server
         * If the server is not connected to internet, it returns 
         * local host ip.
         */
        private string getLocalIP()
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    printLogger("Server ip: " + ip.ToString());
                    return ip.ToString();
                }
            }
            printLogger("Server ip: 127.0.0.1 ");
            return "127.0.0.1";
        }

        /*
         * This function checks if the directory in the given 
         * path exists, if it doesn't it creates a directory
         */
        private void createDirectoryInPath(String path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    printLogger("Creating the directory in the path: " + path);
                }
                else
                {
                    printLogger("Directory already exists in the path: " + path);
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show("Exception occured on directory creation:" + exc.Message);
                return;
            }
        }
      
      /* 
       * This function creates a text database for share operations.
       */ 
        private void createTextDb(String path)
        {
            try
            {
                if (!File.Exists(path + "\\share.txt"))
                {
                    File.Create(path + "\\share.txt");
                    printLogger("Text database created for share operation in path" + path);
                }
                else
                {
                    printLogger("Text database already available");
                }
                pathDb = path + "\\share.txt";
            }
            catch (Exception exc)
            {
                MessageBox.Show("Exception occured on database creation:" + exc.Message);
                return;
            }
        }

        /*
         * Adds file to user entry in order to allow sharing
         */
        public void shareFile(string username, string filename)
        {
            try
            {
                textDbMut.WaitOne();
                System.IO.FileStream fileStream = new System.IO.FileStream(pathDb, FileMode.Open,FileAccess.ReadWrite, FileShare.None);
                System.IO.StreamReader iStream = new System.IO.StreamReader(fileStream);
                System.IO.StreamWriter oStream = new System.IO.StreamWriter(fileStream);
                string line;
                char[] deliminator =" | ".ToCharArray();
                while ((line = iStream.ReadLine()) != null)
                {
                    string[] attributes = line.Split(deliminator);
                    if (username == attributes[0])
                    {
                        fileStream.Seek(-1, SeekOrigin.Current);
                        oStream.WriteLine(" | " + filename);
                        oStream.Flush();
                        printLogger("File sharing entry added");
                        break;
                    }
                }
                fileStream.Close();
                textDbMut.ReleaseMutex();
            }
            catch (Exception exc)
            {
                MessageBox.Show("Exception thrown at file reading: " + exc.Message);
                textDbMut.ReleaseMutex();
                return;
            }
        }

        /*
         * This function creates a user entry in the database
         */
        public void createDbEntryForUser(string username)
        {
            try
            {
                textDbMut.WaitOne();
                System.IO.FileStream fileStream = new System.IO.FileStream(pathDb, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                System.IO.StreamReader iStream = new System.IO.StreamReader(fileStream);
                System.IO.StreamWriter oStream = new System.IO.StreamWriter(fileStream);

                string line;
                char[] deliminator = " | ".ToCharArray();
                while ((line = iStream.ReadLine()) != null)
                {
                    string[] attributes = line.Split(deliminator);
                    if (username == attributes[0])
                    {
                        //username alread available return
                        printLogger("User already exist in database");
                        return;
                    }
                }
                //If username is not avialable write it to db
                printLogger("Creating user entry in database");
                oStream.WriteLine(username);
                oStream.Flush();
                fileStream.Close();
                textDbMut.ReleaseMutex();
            }
            catch (Exception exc)
            {
                MessageBox.Show("Exception thrown at user entry initializing: " + exc.Message);
                textDbMut.ReleaseMutex();
                return;
            }
        }

        /*
      * This function creates a user entry in the database
      */
        public void revokeSharing(string username, string filename)
        {
            try
            {
                textDbMut.WaitOne();
                System.IO.FileStream fileStream = new System.IO.FileStream(pathDb, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                System.IO.StreamReader iStream = new System.IO.StreamReader(fileStream);
                System.IO.StreamWriter oStream = new System.IO.StreamWriter(fileStream);

                string line;
                char[] deliminator = " | ".ToCharArray();
                while ((line = iStream.ReadLine()) != null)
                {
                    string[] attributes = line.Split(deliminator);
                    if (username == attributes[0])
                    {
                        string removedLine = "";
                        printLogger("Removing rights to share");
                        foreach(string attribute in attributes)
                        {
                            if (attribute == filename)
                            {
                                removedLine += " | " + attribute;
                            }
                        }
                        oStream.WriteLine(removedLine);
                        oStream.Flush();
                        break;
                    }
                }
                fileStream.Close();
                textDbMut.ReleaseMutex();
            }
            catch (Exception exc)
            {
                MessageBox.Show("Exception thrown at revoke sharing entry initializing: " + exc.Message);
                textDbMut.ReleaseMutex();
                return;
            }
        }

        /*
         * This function puts an entry in the text database for 
         * sharing the given file with the given user. 
         */
        public void revokeAll(string filename)
        {
            try
            {
                textDbMut.WaitOne();
                System.IO.FileStream fileStream = new System.IO.FileStream(pathDb, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                System.IO.StreamReader iStream = new System.IO.StreamReader(fileStream);
                System.IO.StreamWriter oStream = new System.IO.StreamWriter(fileStream);

                string line;
                char[] deliminator = " | ".ToCharArray();
                while ((line = iStream.ReadLine()) != null)
                {
                    string[] attributes = line.Split(deliminator);
                    string removedLine = "";
                    printLogger("Removing rights to share from everybody for file: "+ filename);
                    foreach (string attribute in attributes)
                    {
                        if (attribute == filename)
                        {
                            removedLine += " | " + attribute;
                        }
                    }
                    oStream.WriteLine(removedLine);
                    oStream.Flush();
                }
                fileStream.Close();
                textDbMut.ReleaseMutex();
            }
            catch (Exception exc)
            {
                MessageBox.Show("Exception thrown at revoke all operation: " + exc.Message);
                textDbMut.ReleaseMutex();
                return;
            }
        }

        /*
         * This function retrieves path and port information from GUI
         * First, it creates a directory, if not exists, 
         * then it binds a socket and puts it in a listening state to
         * given port info.
         */
        private void initalizeListening()
        {
            //Binding the socket to ip and given port name, puts the socket in listening state
            int portNumber = (int)Numeric_Port.Value;
            epLocal = new IPEndPoint(IPAddress.Parse(getLocalIP()), portNumber);
            try
            {
                socket.Bind(epLocal);
                socket.Listen(portNumber);
            }
            catch (Exception exc)
            {
                MessageBox.Show("Exception occured: " + exc.Message);
                return;
            }

            // Sets up the thread which will perform the handshake connection with the client 
            try
            {
                dispatcherThread = new Thread(new ThreadStart(dispatchFileTransferOperations));
                dispatcherThread.IsBackground = true;
                dispatcherThread.Start();
                printLogger("Dispatcher thread starts working.");
                printLogger("Server is now listening on port: " + portNumber);
            }
            catch (Exception exc)
            {
                MessageBox.Show("Thread failed to start: " + exc.Message);
                return;
            }
        }

        /*
         * This function continously listens to given port
         * when a connection comes, it transfers the connection
         * to a new socket that will be handled in a seperate thread.
         * After, thread runs the socket keeps on listening for 
         * new incoming connections.
         */
        private void dispatchFileTransferOperations()
        {
            //Create the database for share operation
            createTextDb(serverPath.Text);

            while (true)
            {
                try
                {
                    Socket dataTransferSocket = socket.Accept();
                    mut.WaitOne();
                    printLogger("A new incomming connection accepted");
                    Thread clientConnectionThread = new Thread(new ParameterizedThreadStart(handleUserConnection));
                    clientConnectionThread.IsBackground = true;
                    clientConnectionThread.Start(dataTransferSocket);
                    mut.ReleaseMutex();
                }
                catch (ThreadInterruptedException exc)
                {
                    socket.Close();
                    printLogger("Thread is stopping listening");
                    break;
                }
                catch (Exception exc)
                {
                    MessageBox.Show("Exception occured while listening: " + exc.Message);
                }
            }
        }

        /*
         * Utility function to check whether socket is still connected
         */
        bool socketConnected(Socket s)
        {
            try
            {
                bool part1 = s.Poll(1000, SelectMode.SelectRead);
                bool part2 = (s.Available == 0);
                if (part1 && part2)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch(Exception exc)
            {
                MessageBox.Show("Exception occured while polling");
            }
            return false;
        }

        /*
         * This function allows users to make multiple file
         * transfer throughout their connection
         */
        private void handleUserConnection(Object socketObj)
        {
            Socket socket = (Socket)socketObj;
            //Recieves the username, filename and filesize to establish the connection
            byte[] handshakeInfo = new byte[128];
            try
            {
                int recievedData = socket.Receive(handshakeInfo);
                //If 0 bytes are recieved connection is dropped
                if (recievedData == 0)
                {
                    MessageBox.Show("Client disconnected");
                    return;
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show("Socket exception occured: " + exc.Message);
                return;
            }

            UTF8Encoding encoder = new UTF8Encoding();
            //Parsing username 
            int usernameSize = BitConverter.ToInt32(handshakeInfo.Take(4).ToArray(), 0);
            string username = encoder.GetString(handshakeInfo.Skip(4).Take(usernameSize).ToArray());
            printLogger("An incoming connection request from user: " + username);

            if (checkUserList(username))
            {
                //Username already has a active connection, terminate
                printLogger("user " + username + "already has an active connection. Terminating connection.");
                termianteUserConnection(socket, username);
                return;
            }

            //Add the username in the list
            connectedUsers.Add(username);
            //Creating database entry for user
            createDbEntryForUser(username);
            sendResultToClient(socket, 0);

            printLogger("Checking for existing directory of user: " + username);
            createDirectoryInPath(serverPath.Text + "\\" + username);

            while (true)
            {
                //If connection terminated exit the loop
                if (!socketConnected(socket))
                {
                    removeFromUserList(username);
                    break;
                }
                byte[] operationInfo = new byte[128];
                try
                {
                    int recievedData = socket.Receive(operationInfo);
                    //If 0 bytes are recieved connection is dropped
                    if (recievedData == 0)
                    {
                        MessageBox.Show("Client disconnected");
                        removeFromUserList(username);
                        return;
                    }
                }
                catch (Exception exc)
                {
                    MessageBox.Show("Socket exception occured: " + exc.Message);
                    removeFromUserList(username);
                    return;
                }

                //Parsing operation type
                //BRW,DEL,RNM,DWN,UPL are possible operation types
                //Parsing username  
                string operation = encoder.GetString(operationInfo.Take(3).ToArray());

                switch (operation)
                {
                    //Downloading shared file operation
                    case "DSH":
                        int filenameSize = BitConverter.ToInt32(operationInfo.Skip(3).Take(4).ToArray(), 0);
                        string filename = encoder.GetString(operationInfo.Skip(3 + 4).Take(filenameSize).ToArray());
                        int ownerNameSize = BitConverter.ToInt32(operationInfo.Skip(3 + 4 + filenameSize).Take(4).ToArray(), 0);
                        string ownerName = encoder.GetString(operationInfo.Skip(3 + 4 + filenameSize + 4).Take(ownerNameSize).ToArray());
                        dataTransferToClient(socketObj, ownerName, filename);
                        break;
                    //File sharing operation
                    case "SHR":
                        filenameSize = BitConverter.ToInt32(operationInfo.Skip(3).Take(4).ToArray(), 0);
                        filename = encoder.GetString(operationInfo.Skip(3 + 4).Take(filenameSize).ToArray());
                        int userToShareSize = BitConverter.ToInt32(operationInfo.Skip(3 + 4 + filenameSize).Take(4).ToArray(), 0);
                        string userToShare = encoder.GetString(operationInfo.Skip(3 + 4 + filenameSize + 4).Take(userToShareSize).ToArray());
                        shareFileWithUser(username, username + "\\" + filename);
                        break;
                    //File sharing operation
                    case "RVK":
                        filenameSize = BitConverter.ToInt32(operationInfo.Skip(3).Take(4).ToArray(), 0);
                        filename = encoder.GetString(operationInfo.Skip(3 + 4).Take(filenameSize).ToArray());
                        int userToRevokeSize = BitConverter.ToInt32(operationInfo.Skip(3 + 4 + filenameSize).Take(4).ToArray(), 0);
                        string userToRevoke = encoder.GetString(operationInfo.Skip(3 + 4 + filenameSize + 4).Take(userToRevokeSize).ToArray());
                        revokeSharingeWithUser(username, filename);
                        break;
                    //File upload operation
                    case "UPL":
                        transferData(socketObj, username);
                        break;
                    //File list browse operation
                    case "BRW":
                        requestFileList(socketObj, username);
                        break;
                    //File delete operation
                    case "DEL":
                        //Parse the file to be deleted
                        filenameSize = BitConverter.ToInt32(operationInfo.Skip(3).Take(4).ToArray(), 0);
                        filename = encoder.GetString(operationInfo.Skip(3 + 4).Take(filenameSize).ToArray());
                        deleteFile(socketObj, username, filename);
                        break;
                    //File rename operation
                    case "RNM":
                        //Parse the file to be renamed
                        filenameSize = BitConverter.ToInt32(operationInfo.Skip(3).Take(4).ToArray(), 0);
                        filename = encoder.GetString(operationInfo.Skip(3 + 4).Take(filenameSize).ToArray());
                        int newfilenameSize = BitConverter.ToInt32(operationInfo.Skip(3 + 4 + filenameSize).Take(4).ToArray(), 0);
                        string newFilename = encoder.GetString(operationInfo.Skip(3 + 4 + filenameSize + 4).Take(newfilenameSize).ToArray());
                        renameFile(socketObj, username, filename, newFilename);
                        break;
                    //File download operation
                    case "DWN":
                        //Parse the file to be downloaded
                        filenameSize = BitConverter.ToInt32(operationInfo.Skip(3).Take(4).ToArray(), 0);
                        filename = encoder.GetString(operationInfo.Skip(3 + 4).Take(filenameSize).ToArray());
                        dataTransferToClient(socketObj, username, filename);
                        break;
                    default:
                        printLogger("Unknown operation type");
                        break;
                }
            }
        }

        /*
         * This function sends a file list to the client 
         * The list contains name, size, upload time of each file
         */
        private void requestFileList(Object socketObj, string username)
        {
            Socket socket = (Socket)socketObj;
            string directoryPath = serverPath.Text + "\\" + username;
            string[] files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);

            //Fill the list with name,size,upload time of the files
            string list = "";
            foreach (string filename in files)
            {
                try
                {
                    FileInfo fileInfo = new System.IO.FileInfo(filename);
                    list += filename.Remove(0, directoryPath.Length + 1) + " " + fileInfo.Length + " " + fileInfo.LastWriteTime + "\n";
                }
                catch (Exception exc)
                {
                    MessageBox.Show("Exception occured during view file operation for user: " + username + exc.Message);
                    MessageBox.Show(directoryPath + "\\" + filename);
                }
                
            }
            if (list == "")
            {
                list = "No file found";
            }
            //Convert the list into a byte array, get the size of it
            UTF8Encoding encoder = new UTF8Encoding();
            byte[] listInBytes = encoder.GetBytes(list);
            byte[] result = new byte[1024];
            //Send the list to the client
            printLogger("Server is sending the requested list view...");
            try
            {
                socket.Send(listInBytes);
            }
            catch(Exception exc)
            {
                MessageBox.Show("Client disconnected : "+ exc.Message);
            }
            printLogger("List view is sent succesfully.");
        }

        /*
         * This function deletes the file of the given user
         * It notifies the client about the completion of the operation
         */
        private void deleteFile(Object socketObj, string username, string filename)
        {
            Socket socket = (Socket)socketObj;
            string directoryPath = serverPath.Text + "\\" + username;
            string filePath = directoryPath + "\\" + filename;

            try
            {
                if (File.Exists(filePath))
                {           
                    File.Delete(filePath);
                    revokeAll(username + "\\" + filename);
                    sendResultToClient(socket, 0);
                }else
                { 
                    //File deletion is successful
                    sendResultToClient(socket, 1);
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show("Exception occured during delete file operation for user: " + username + exc.Message);
                sendResultToClient(socket, 1);
            }
        }

        /*
         * This function changes the name of the file with the given filename
         * to newFilename. It notifies the client about the result of the operation
         */
        private void renameFile(Object socketObj, string username, string filename, string newFilename)
        {
            Socket socket = (Socket)socketObj;
            string directoryPath = serverPath.Text + "\\" + username;
            string filePath = directoryPath + "\\" + filename;
            string newFilePath = directoryPath + "\\" + newFilename;

            try
            {
                if (File.Exists(filePath))
                {
                    //If the new name already exists in server abort
                    if (File.Exists(newFilePath))
                    {
                        sendResultToClient(socket, 1);
                        return;
                    }

                    File.Move(filePath, newFilePath);
                    revokeAll(username + "\\" + filename);
                    //File rename is successful
                    sendResultToClient(socket, 0);
                }
                else
                {
                    //File not exist notify client
                    sendResultToClient(socket, 1);
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show("Exception occured during rename file operation for user: " + username + exc.Message);
                sendResultToClient(socket, 1);
                return;
            }  
        }

        /*
         * This function sends the file with the given filename to the client
         * on the end of the socketObj by first sending the filesize, then transfering
         * the data.
         */
        private void dataTransferToClient(Object socketObj, string username, string filename)
        {
            Socket socket = (Socket)socketObj;
            string directoryPath = serverPath.Text + "\\" + username;
            string filePath = directoryPath + "\\" + filename;
            printLogger("Server is sending the file " + filename + " for user: " + username);

            if (!File.Exists(filePath))
            {
                //No file found
                socket.Send(BitConverter.GetBytes(0));
                return;
            }

            //Sending file size beforehand
            long filesize = new FileInfo(filePath).Length;
            byte[] filesizeInBytes = BitConverter.GetBytes(filesize);           
            //Sending the file
            try
            {
                socket.Send(filesizeInBytes);
                socket.BeginSendFile(filePath, new AsyncCallback(FileSendCallback), socket);
            }
            catch (Exception exc)
            {
                MessageBox.Show("Exception occured during data transfer: " + exc.Message);
                return;
            }
        }

        /*
         * This function puts an entry in the text database for 
         * sharing the given file with the given user. 
         */
        public void shareFileWithUser(string username, string filename)
        {
            shareFile(username, filename);
        }

        /*
         * This function removes the share database entry from the 
         * share database to revoke user sharing rights.
         */ 
        public void revokeSharingeWithUser(string username, string filename)
        {
            revokeSharing(username, filename);
        }

        private void FileSendCallback(IAsyncResult ar)
        {
            // Retrieve the socket from the state object.
            Socket clientSocket = (Socket)ar.AsyncState;
            try
            {
                clientSocket.EndSendFile(ar);
                // Complete sending the data to the remote device.
                printLogger("File transfer complete");
            }
            catch (Exception exc)
            {
                MessageBox.Show("Socket exception occured.");
                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Close();
            }
        }

        /*
         * This function takes a socket object as a parameter. 
         * First, it recieves the handshake information from the 
         * client, which contains username size,username, filename size,
         * filename, filesize. Then, it verifies that this is a unique client
         * by checking it on the namelist. If it verifies, it initiates data transfer
         * over TCP sockets.
         */
        private bool transferData(Object socketObj, string username)
        {
            Socket socket = (Socket)socketObj;

            byte[] fileInfo = new byte[128];
            try
            {
                int recievedData = socket.Receive(fileInfo);
                //If 0 bytes recieved socket connection is lost
                if (recievedData == 0)
                {
                    MessageBox.Show("Client disconnected");
                    termianteUserConnection(socket, username);
                    return false;
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show("Socket exception occured: " + exc.Message);
                return false;
            }

            UTF8Encoding encoder = new UTF8Encoding();
            //Parsing filename and fileSize
            int filenameSize = BitConverter.ToInt32(fileInfo.Take(4).ToArray(), 0);
            string filename = encoder.GetString(fileInfo.Skip(4).Take(filenameSize).ToArray());
            long fileSize = BitConverter.ToInt64(fileInfo.Skip(4 + filenameSize).Take(sizeof(long)).ToArray(), 0);

            printLogger("File transfer started for user: " + username + " \n" + "Filesize: " + fileSize);

            /*
             * Recieves the file in chunks of 2KB, it continues to recieve until 
             * the file transfer is completed.
             */
            byte[] data = new byte[8 * 1024];
            //If a file is reupload revoke all the sharing
            if (File.Exists(serverPath.Text + "\\" + username + "\\" + filename))
            {
                revokeAll(username + "\\" + filename);
            }
             
            FileStream stream = File.Create(serverPath.Text + "\\" + username + "\\" + filename);
            try
            {
                long bytesLeftToTransfer = fileSize;
                printLogger("Filesize: " + fileSize);

                while (bytesLeftToTransfer > 0)
                {
                    long amountOfBytes = socket.Receive(data);
                    //If 0 bytes is recieved socket connection is dropped
                    if (amountOfBytes == 0)
                    {
                        MessageBox.Show("Client disconnected");
                        throw new SocketException();
                    }
                    long bytesToCopy = Math.Min(amountOfBytes, bytesLeftToTransfer);
                    stream.Write(data, 0, (int)bytesToCopy);
                    bytesLeftToTransfer -= bytesToCopy;
                }
                printLogger("Stream closed for " + username);
                stream.Close();
            }
            catch (SocketException exc)
            {
                MessageBox.Show("Socket Exception occured");
                stream.Close();
                File.Delete(serverPath.Text + "\\" + username + "\\" + filename);
                printLogger("Corrupted filed is being deleted ");
                termianteUserConnection(socket, username);
                return false;
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
                termianteUserConnection(socket, username);
                return false;
            }
            printLogger("File transfer finished for user:" + username);
            return true;
        }


        /*
         *  This method terminates the sockets 
         */
        private void terminateSocket(Socket sck)
        {
            sck.Shutdown(SocketShutdown.Both);
            sck.Close();
        }

        private void Button_Start_Click(object sender, EventArgs e)
        {
            if (Button_Start.Text == "Start Listening")
            {
                initalizeListening();
               /* Button_Start.Enabled = false;
                Button_Stop.Enabled = true;
                Button_Start.Text = "Stop Listening";*/
            }
            else
            {
                int clientnumber = connectedUsers.Count;
                if (clientnumber > 0)
                {
                    DialogResult DR = MessageBox.Show("There are " + clientnumber + " client(s) connected to server.\n Are you sure ?", "WARNING", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
                    if (DR == DialogResult.Yes)
                    {
                        connectedUsers.Clear();
                        terminateSocket(socket);
                        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        printLogger("Server stopped listening at port " + Numeric_Port.Value + ".\r\n");
                        Button_Start.Text = "Start Listening";
                    }
                }
            }
        }

        private void serverForm_Load(object sender, EventArgs e)
        {
            TextBox.CheckForIllegalCrossThreadCalls = false;
            Button_Stop.Enabled = false;
            Numeric_Port.Value = 8888;
            //Creating a socket to listen the incoming requests
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);           
        }

        /*
         * This method iterates over the user list to find out 
         * whether the username already exists on the list
         */
        private bool checkUserList(string nameToCheck)
        {
            foreach (string clientName in connectedUsers)
            {
                if (clientName.Equals(nameToCheck))
                {
                    // User name exists in the list return true
                    return true;
                }
            }
            // Iterated over all of the list, username is not in the list
            return false;
        }

        private void removeFromUserList(string username)
        {
            connectedUsers.Remove(username);
        }

        private void termianteUserConnection(Socket socket, string username)
        {
            sendResultToClient(socket, 1);
            terminateSocket(socket);
            removeFromUserList(username);
        }

        private void sendResultToClient(Socket socket, Int32 result)
        {
            socket.Send(BitConverter.GetBytes(result));
        }

        private void serverBrowse_Click(object sender, EventArgs e)
        {
            string folderPath = "";
            FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                folderPath = folderBrowserDialog1.SelectedPath;
                serverPath.Text = folderPath;
                printLogger("You chose the path " + folderPath + " to upload items.");
            }
        }

        private void serverForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            int clientnumber = connectedUsers.Count;
            if (clientnumber > 0)
            {
                DialogResult DR = MessageBox.Show("There are " + clientnumber + " client(s) connected to server.\n Are you sure ?", "WARNING", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
                if (DR == DialogResult.Yes)
                {
                    System.Environment.Exit(1);
                }
                else
                {
                    e.Cancel = true;
                }
            }
            else if (clientnumber == 0)
            {
                DialogResult DR = MessageBox.Show("Are you sure ?", "Server ShutDown", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (DR == DialogResult.Yes)
                {
                    System.Environment.Exit(1);
                }
                else
                    e.Cancel = true;
            }
        }

        private void Button_Stop_Click_1(object sender, EventArgs e)
        {
            printLogger("Server stopping listening");
            mut.WaitOne();
            dispatcherThread.Interrupt();
            mut.ReleaseMutex();

            Button_Stop.Enabled = false;
            Button_Start.Enabled = true;
        }

        private void sendNotificationToClient(ClientConnection client,string message)
        {
            UTF8Encoding encoder = new UTF8Encoding();
            byte[] messageInBytes = encoder.GetBytes(message);
            client.mutex.WaitOne();
            try
            {
                client.socket.Send(messageInBytes);
            }
            catch (Exception exc)
            {
                MessageBox.Show("Exception occured on sending notification to the client: " + client.username);
            }
            client.mutex.ReleaseMutex();
        }
    }
}