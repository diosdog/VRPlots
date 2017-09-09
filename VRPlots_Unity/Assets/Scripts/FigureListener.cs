using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using MTypes;


public class FigureListener : MonoBehaviour {

    public string IP = "127.0.0.1";
    public int port = 21241;

    TcpListener server;
    
    Socket socket;
    
    NetworkStream stream;
    int rxBufferSize = 1 << 15;

    //private bool isConnected = false;
    private bool hasOpenConnection = false;
    private JsonSerializer serializer;

    // Use this for initialization
	void Start () {
        serializer = new JsonSerializer();
        serializer.Converters.Add(new MFigureConverter());
        serializer.Converters.Add(new GameObjectConverter());
        serializer.Converters.Add(new MPatch.PatchColorConverter());
        serializer.Converters.Add(new MMatrixOrVecConverter<float>());
        serializer.DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate;
        //serializer.MissingMemberHandling = MissingMemberHandling.Error;
        //serializer.CheckAdditionalContent = true;

        server = new TcpListener(IPAddress.Parse(IP), port);

        server.Start(); // start listening for clients
    }

    // Update is called once per frame
   	void Update () {

        if (!hasOpenConnection)
        {
            if (server.Pending())
            {
                hasOpenConnection = true;
                StartCoroutine(Connect());
            }   
        }
	}

    IEnumerator Connect()
    {
        // this is more convoluted than it otherwise would be because we can't put yield statements inside try/catch

        //TODO: rewrite as server

        if (!server.Pending())
        {
            // if no connections pending, trying to accept a connection would block
            hasOpenConnection = false;
            yield break;
        }

        Socket socket = null;
        
        var didConnect = false;
        var waitingForConnect = true;

        new Thread(() =>
        {
            try
            {
                socket = server.AcceptSocket();
                didConnect = true;
                waitingForConnect = false;
            } catch (SocketException e)
            {
                Debug.LogError("Caught socketException in thread");
                didConnect = false;
                waitingForConnect = false;
            }
        }).Start();

        while(waitingForConnect)
        {
            // wait for connection to finish without blocking
            yield return new WaitForSecondsRealtime(0.1f);
        }

        if (!didConnect)
        {
            hasOpenConnection = false;
            yield break;
        }
        
        // prevent socket receive calls from blocking
        socket.ReceiveTimeout = 10; // in ms

        /* expected message is in format:
         * [startToken] (string)
         * [numberOfBytes] (uint32)
         * [body of message...
         *  ...
         *  ... ] (numberOfBytes long)
         * [endToken]
         */

        var startToken = "StartOfFigure";
        var endToken = "EndOfFigure";

        var messagesToParse = new List<string>();

        var didTimeout = false;
        while (true)
        {
            StringBuilder resp = new StringBuilder();

            Byte[] respData = new Byte[rxBufferSize];

            var timeoutCounter = 0;

            var didError = false;

            // look for startToken
            while (true)
            {
                var numBytesNeeded = startToken.Length - resp.Length;
                if (socket.Available >= numBytesNeeded)
                {
                    timeoutCounter = 0;
                    var numBytes = socket.Receive(respData, numBytesNeeded, SocketFlags.None);
                    Debug.Assert(numBytes == numBytesNeeded);
                    resp.Append(Encoding.ASCII.GetString(respData, 0, numBytes));
                    Debug.Assert(resp.Length == startToken.Length);
                    if (resp.ToString() == startToken)
                        break;
                    // else what we have is not the start token

                    //TODO: could make this more efficient by discarding everything up until the next occurence of the first character of start token, instead of
                    // re-reading one byte extra at a time when scanning through non-start data

                    //TODO: could maybe make this more efficient by reading all available bytes into resp buffer then scanning through for start token 
                    // (reducing number of socket.Receive calls)

                    resp.Remove(0, 1);
                }
                else
                {
                    timeoutCounter++;
                    if (timeoutCounter > 1000)
                    {
                        Debug.LogWarning("Timed out waiting for start token");
                        didTimeout = true;
                        break;
                    }
                    yield return new WaitForSecondsRealtime(0.1f);
                    continue;
                }
            }
            if (didTimeout)
                break;

            // next 4 bytes should be uint32 specifying length of body of message
            UInt32 numBytesInMessage = 0;
            while (true)
            {
                var numBytesNeeded = 4;
                if (socket.Available >= numBytesNeeded)
                {
                    timeoutCounter = 0;
                    var numBytes = socket.Receive(respData, numBytesNeeded, SocketFlags.None);
                    Debug.Assert(numBytes == numBytesNeeded);
                    numBytesInMessage = BitConverter.ToUInt32(respData, 0);
                    Debug.Assert(numBytesInMessage > 0);
                    Debug.LogFormat("Reading message with {0} bytes", numBytesInMessage);
                    break;
                }
                else
                {
                    timeoutCounter++;
                    if (timeoutCounter > 100)
                    {
                        Debug.LogWarning("Timed out waiting for length bytes");
                        didTimeout = true;
                        break;
                    }
                    yield return new WaitForSecondsRealtime(0.1f);
                    continue;
                }
            }
            if (didTimeout)
                break;

            // next numBytesInMessage bytes should be body of message
            resp.Remove(0, resp.Length); // clear previous string (just startToken)
            while (true)
            {
                var numBytesNeeded = (int)Mathf.Min(rxBufferSize, numBytesInMessage - resp.Length);
                if (socket.Available >= numBytesNeeded)
                {
                    timeoutCounter = 0;
                    var numBytes = socket.Receive(respData, numBytesNeeded, SocketFlags.None);
                    Debug.Assert(numBytes == numBytesNeeded);
                    resp.Append(Encoding.ASCII.GetString(respData, 0, numBytes));
                    if (resp.Length >= numBytesInMessage)
                    {
                        break;
                    }
                }
                else
                {
                    timeoutCounter++;
                    if (timeoutCounter > 100)
                    {
                        Debug.LogWarningFormat("Timed out waiting for message body: {0}/{1} received, {2} waiting",resp.Length,numBytesInMessage,socket.Available);
                        didTimeout = true;
                        break;
                    }
                    yield return new WaitForSecondsRealtime(0.1f);
                    continue;
                }
            }
            if (didTimeout)
                break;

            // next should be endtoken
            while (true)
            {
                var numBytesNeeded = endToken.Length;
                if (socket.Available >= numBytesNeeded)
                {
                    timeoutCounter = 0;
                    var numBytes = socket.Receive(respData, numBytesNeeded, SocketFlags.None);
                    Debug.Assert(numBytes == numBytesNeeded);
                    var shouldBeEndToken = Encoding.ASCII.GetString(respData, 0, numBytes);
                    if (shouldBeEndToken == endToken)
                        break;
                    else
                    {
                        Debug.LogErrorFormat("Received {0} rather than endToken {1}", shouldBeEndToken, endToken);
                        didError = true;
                        break;
                    }
                }
                else
                {
                    timeoutCounter++;
                    if (timeoutCounter > 100)
                    {
                        Debug.LogWarning("Timed out waiting for end token");
                        didTimeout = true;
                        break;
                    }
                    yield return new WaitForSecondsRealtime(0.1f);
                    continue;
                }
            }
            if (didTimeout)
                break;
            else if (didError)
                continue; // go back to looking for start

            messagesToParse.Add(resp.ToString());

            // send acknowledgement of receipt
            
            socket.Send(System.Text.Encoding.ASCII.GetBytes("ack"));

            yield return new WaitForSecondsRealtime(0.1f);

            if (socket.Available > 0)
                continue; // start reading another figure 
            else
                break; // start parsing current figure
        }

        if (didTimeout)
        {
            Debug.LogWarning("Timed out");
        }

        socket.Close();
        hasOpenConnection = false;
        
        foreach(string message in messagesToParse)
        {
            //Debug.Log("About to parse figure");
            ParseJSON(message);
        }
    }

    void ParseJSON(string rawJSON)
    {
        StringReader sr = new StringReader(rawJSON);
        var reader = new JsonTextReader(sr);

        // assume raw JSON is a single figure
        //Debug.Log("Deserializing figure");
        MFigure fig = serializer.Deserialize<MFigure>(reader);

        fig.init();
        //Debug.LogFormat("Deserialized to: {0}", fig);
    }
}


// only for converting Mtype GameObjects (MAxes, MLine, etc.)
public class GameObjectConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(GameObject).IsAssignableFrom(objectType);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        //Debug.Log("In ReadJson for GameObjectConverter");

        var token = JToken.Load(reader);

        if (token["Position"] != null)
        {
            //Debug.LogFormat("Position: {0}", token["Position"]);
        }

        if (token["ObjectClass"] == null)
        {
            //Debug.Log("ObjectClass not specified. Cannot deserialize.");
            return null;
        }

        GameObject go = new GameObject();
        switch((string) token["ObjectClass"])
        {
            case MGroup.classStr:
                {
                    //Debug.Log("Deserializing MGroup");
                    var obj = go.AddComponent<MGroup>();
                    go.name = "MGroup";
                    serializer.Populate(token.CreateReader(), obj);
                    obj.LinkChildren();
                    break;
                }
            case MText.classStr:
                {
                    //Debug.Log("Deserializing MText");
                    var obj = go.AddComponent<MText>();
                    go.name = "MText";
                    serializer.Populate(token.CreateReader(), obj);
                    obj.LinkChildren();
                    break;
                }
            case MAxes.classStr:
                {
                    //Debug.Log("Deserializing MAxes");
                    var obj = go.AddComponent<MAxes>();
                    go.name = "MAxes";
                    serializer.Populate(token.CreateReader(), obj);
                    obj.LinkChildren();
                    break;
                }
            case MLine.classStr:
            case MLine.altClassStr:
                {
                    //Debug.Log("Deserializing MLine");
                    var obj = go.AddComponent<MLine>();
                    go.name = "MLine";
                    serializer.Populate(token.CreateReader(), obj);
                    obj.LinkChildren();
                    break;
                }
            case MScatter.classStr:
                {
                    //Debug.Log("Deserializing MScatter");
                    var obj = go.AddComponent<MScatter>();
                    go.name = "MScatter";
                    serializer.Populate(token.CreateReader(), obj);
                    obj.LinkChildren();
                    break;
                }
            case MPatch.classStr:
                {
                    //Debug.Log("Deserializing MPatch");
                    var obj = go.AddComponent<MPatch>();
                    go.name = "MPatch";
                    serializer.Populate(token.CreateReader(), obj);
                    obj.LinkChildren();
                    break;
                }
            default:
                Debug.LogErrorFormat("Unrecognized ObjectClass: {0}", token["ObjectClass"]);
                break;
        }
        return go;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}

