
//*********************************************************************************************************
// Written by Gary Kiebel and Dave Clark for the US Department of Energy
// Pacific Northwest National Laboratory, Richland, WA
// Copyright 2009, Battelle Memorial Institute
// Created 06/26/2009
//*********************************************************************************************************

using System;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Apache.NMS.ActiveMQ.Commands;

namespace PkgFolderCreateManager
{
    // received commands are sent to a delegate function with this signature
    public delegate void MessageProcessorDelegate(string cmdText);

    /// <summary>
    /// Handles sanding and receiving of control and status messages
    /// </summary>
    /// <remarks>Base code provided by Gary Kiebel</remarks>
    class clsMessageHandler : IDisposable
    {

        #region "Class variables"

        private string m_BrokerUri;
        private string m_CommandQueueName;
        private string m_BroadcastTopicName;
        private string m_StatusTopicName;
        private clsMgrSettings m_MgrSettings;

        private IConnection m_Connection;
        private ISession m_StatusSession;
        private IMessageProducer m_StatusSender;
        private IMessageConsumer m_CommandConsumer;
        private IMessageConsumer m_BroadcastConsumer;

        private bool m_IsDisposed;
        private bool m_HasConnection;

        #endregion

        #region "Events"

        public event MessageProcessorDelegate CommandReceived;
        public event MessageProcessorDelegate BroadcastReceived;

        #endregion

        #region "Properties"

        public clsMgrSettings MgrSettings
        {
            set => m_MgrSettings = value;
        }

        public string BrokerUri
        {
            get => m_BrokerUri;
            set => m_BrokerUri = value;
        }

        public string CommandQueueName
        {
            get => m_CommandQueueName;
            set => m_CommandQueueName = value;
        }

        public string BroadcastTopicName
        {
            get => m_BroadcastTopicName;
            set => m_BroadcastTopicName = value;
        }

        public string StatusTopicName
        {
            get => m_StatusTopicName;
            set => m_StatusTopicName = value;
        }

        #endregion

        #region "Methods"

        /// <summary>
        /// create set of NMS connection objects necessary to talk to the ActiveMQ broker
        /// </summary>
        protected void CreateConnection()
        {
            if (m_HasConnection) return;
            try
            {
                IConnectionFactory connectionFactory = new ConnectionFactory(m_BrokerUri);
                m_Connection = connectionFactory.CreateConnection();
                m_Connection.Start();

                m_HasConnection = true;
                // temp debug
                // Console.WriteLine("--- New connection made ---" + Environment.NewLine); //+ e.ToString()
                var msg = "Connected to broker";
                clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, msg);
            }
            catch (Exception ex)
            {
                // we couldn't make a viable set of connection objects
                // - this has "long day" written all over it,
                // but we don't have to do anything specific at this point (except eat the exception)

                // Console.WriteLine("=== Error creating connection ===" + Environment.NewLine); //+ e.ToString() // temp debug
                var msg = "Exception creating broker connection";
                LogError(msg, ex);
            }
        }

        /// <summary>
        /// Create the message broker communication objects and register the listener function
        /// </summary>
        /// <returns>TRUE for success; FALSE otherwise</returns>
        public bool Init()
        {
            try
            {
                if (!m_HasConnection) CreateConnection();
                if (!m_HasConnection) return false;

                // queue for "make folder" commands from database via its STOMP message sender
                var commandSession = m_Connection.CreateSession();
                m_CommandConsumer = commandSession.CreateConsumer(new ActiveMQQueue(m_CommandQueueName));
                //                    commandConsumer.Listener += new MessageListener(OnCommandReceived);
                LogDebug("Command listener established");

                // topic for commands broadcast to all folder makers
                var broadcastSession = m_Connection.CreateSession();
                m_BroadcastConsumer = broadcastSession.CreateConsumer(new ActiveMQTopic(m_BroadcastTopicName));
                //                    broadcastConsumer.Listener += new MessageListener(OnBroadcastReceived);
                LogDebug("Broadcast listener established");

                // topic for the folder maker to send status information over
                m_StatusSession = m_Connection.CreateSession();
                m_StatusSender = m_StatusSession.CreateProducer(new ActiveMQTopic(m_StatusTopicName));
                LogDebug("Status sender established");

                return true;
            }
            catch (Exception ex)
            {
                LogError("Exception while initializing message sessions", ex);
                DestroyConnection();
                return false;
            }
        }

        /// <summary>
        /// Command listener function. Received commands will cause this to be called
        ///    and it will trigger an event to pass on the command to all registered listeners
        /// </summary>
        /// <param name="message">Incoming message</param>
        private void OnCommandReceived(IMessage message)
        {
            var textMessage = message as ITextMessage;
            var Msg = "clsMessageHandler(), Command message received";
            clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, PRISM.Logging.BaseLogger.LogLevels.DEBUG, Msg);
            if (CommandReceived != null)
            {
                // call the delegate to process the commnd
                Msg = "clsMessageHandler().OnCommandReceived: At lease one event handler assigned";
                clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, PRISM.Logging.BaseLogger.LogLevels.DEBUG, Msg);
                if (textMessage != null)
                {
                    CommandReceived(textMessage.Text);
                }
            }
            else
            {
                Msg = "clsMessageHandler().OnCommandReceived: No event handlers assigned";
                clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, PRISM.Logging.BaseLogger.LogLevels.DEBUG, Msg);
            }
        }

        /// <summary>
        /// Broadcast listener function. Received Broadcasts will cause this to be called
        ///    and it will trigger an event to pass on the command to all registered listeners
        /// </summary>
        /// <param name="message">Incoming message</param>
        private void OnBroadcastReceived(IMessage message)
        {
            var textMessage = message as ITextMessage;
            var Msg = "clsMessageHandler(), Broadcast message received";
            clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, PRISM.Logging.BaseLogger.LogLevels.DEBUG, Msg);
            if (BroadcastReceived != null)
            {
                // call the delegate to process the commnd
                Msg = "clsMessageHandler().OnBroadcastReceived: At lease one event handler assigned";
                clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, PRISM.Logging.BaseLogger.LogLevels.DEBUG, Msg);
                if (textMessage != null)
                {
                    BroadcastReceived(textMessage.Text);
                }
            }
            else
            {
                Msg = "clsMessageHandler().OnBroadcastReceived: No event handlers assigned";
                clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, PRISM.Logging.BaseLogger.LogLevels.DEBUG, Msg);
            }
        }

        /// <summary>
        /// Sends a status message
        /// </summary>
        /// <param name="message">Outgoing message string</param>
        public void SendMessage(string message)
        {
            if (!m_IsDisposed)
            {
                var textMessage = m_StatusSession.CreateTextMessage(message);
                textMessage.Properties.SetString("ProcessorName", m_MgrSettings.GetParam("MgrName"));
                m_StatusSender.Send(textMessage);
            }
            else
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        #endregion

        #region "Cleanup"

        /// <summary>
        /// Cleans up a connection after error or when closing
        /// </summary>
        protected void DestroyConnection()
        {
            if (m_HasConnection)
            {
                m_Connection.Dispose();
                m_HasConnection = false;
                var msg = "Message connection closed";
                clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, msg);
            }
        }

        /// <summary>
        /// Implements IDisposable interface
        /// </summary>
        public void Dispose()
        {
            if (!m_IsDisposed)
            {
                DestroyConnection();
                m_IsDisposed = true;
            }
        }

        /// <summary>
        /// Registers the command and broadcast listeners under control of main program.
        /// This is done to prevent loss of queued messages if listeners are registered too early.
        /// </summary>
        public void RegisterListeners()
        {
            m_CommandConsumer.Listener += OnCommandReceived;
            m_BroadcastConsumer.Listener += OnBroadcastReceived;
        }

        #endregion
    }
}
