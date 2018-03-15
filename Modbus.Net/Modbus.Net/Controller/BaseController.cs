using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Modbus.Net
{
    /// <summary>
    ///     ����������
    /// </summary>
    public abstract class BaseController : IController
    { 
        /// <summary>
        ///     �ȴ�����Ϣ����
        /// </summary>
        protected List<MessageWaitingDef> WaitingMessages { get; set; }

        /// <summary>
        ///     ��Ϣά���߳�
        /// </summary>
        protected Task SendingThread { get; set; }

        /// <summary>
        ///     ���з�λ��
        /// </summary>
        protected Func<byte[], ICollection<byte[]>> DuplicateFunc { get; }

        /// <summary>
        ///     ������
        /// </summary>
        /// <param name="duplicateFunc">���зֺ���</param>
        protected BaseController(Func<byte[], ICollection<byte[]>> duplicateFunc = null)
        {
            WaitingMessages = new List<MessageWaitingDef>();
            DuplicateFunc = duplicateFunc;
        }

        /// <inheritdoc />
        public MessageWaitingDef AddMessage(byte[] sendMessage)
        {
            var def = new MessageWaitingDef
            {
                Key = GetKeyFromMessage(sendMessage)?.Item1,
                SendMessage = sendMessage,
                SendMutex = new AutoResetEvent(false),
                ReceiveMutex = new AutoResetEvent(false)
            };
            if (AddMessageToList(def))
            {
                return def;
            }
            return null;
        }

        /// <summary>
        ///     ������Ϣ��ʵ���ڲ�����
        /// </summary>
        protected abstract void SendingMessageControlInner();

        /// <inheritdoc />
        public abstract void SendStop();

        /// <inheritdoc />
        public virtual void SendStart()
        {
            if (SendingThread == null)
            {
                SendingThread = Task.Run(()=>SendingMessageControlInner());               
            }
        }

        /// <inheritdoc />
        public void Clear()
        {
            lock (WaitingMessages)
            {
                WaitingMessages.Clear();
            }
        }

        /// <summary>
        ///     ����Ϣ���ӵ�����
        /// </summary>
        /// <param name="def">��Ҫ���ӵ���Ϣ��Ϣ</param>
        protected virtual bool AddMessageToList(MessageWaitingDef def)
        {
            lock (WaitingMessages)
            {
                if (WaitingMessages.FirstOrDefault(p => p.Key == def.Key) == null || def.Key == null)
                {
                    WaitingMessages.Add(def);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        ///     ��ȡ��Ϣ�ļ����ؼ���
        /// </summary>
        /// <param name="message">��ȷ�ϵ���Ϣ</param>
        /// <returns>��Ϣ�ļ����ؼ���</returns>
        protected abstract (string,string)? GetKeyFromMessage(byte[] message);

        /// <inheritdoc />
        public ICollection<(byte[], bool)> ConfirmMessage(byte[] receiveMessage)
        {
            var ans = new List<(byte[], bool)>();
            var duplicatedMessages = DuplicateFunc?.Invoke(receiveMessage);
            duplicatedMessages = duplicatedMessages ?? new List<byte[]> {receiveMessage};
            foreach (var message in duplicatedMessages)
            {
                var def = GetMessageFromWaitingList(message);
                if (def != null)
                {
                    def.ReceiveMessage = receiveMessage;
                    lock (WaitingMessages)
                    {
                        WaitingMessages.Remove(def);
                    }
                    def.ReceiveMutex.Set();
                    ans.Add((message, true));
                }
                ans.Add((message, false));
            }
            return ans;
        }

        

        /// <summary>
        ///     �ӵȴ�������ƥ����Ϣ
        /// </summary>
        /// <param name="receiveMessage">���ص���Ϣ</param>
        /// <returns>�ӵȴ�������ƥ�����Ϣ</returns>
        protected abstract MessageWaitingDef GetMessageFromWaitingList(byte[] receiveMessage);

        /// <inheritdoc />
        public void ForceRemoveWaitingMessage(MessageWaitingDef def)
        {
            lock (WaitingMessages)
            {
                WaitingMessages.Remove(def);
            }
        }
    }

    /// <summary>
    ///     �ȴ���Ϣ�Ķ���
    /// </summary>
    public class MessageWaitingDef
    {
        /// <summary>
        ///     ��Ϣ�Ĺؼ���
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        ///     ���͵���Ϣ
        /// </summary>
        public byte[] SendMessage { get; set; }

        /// <summary>
        ///     ���յ���Ϣ
        /// </summary>
        public byte[] ReceiveMessage { get; set; }

        /// <summary>
        ///     ���͵��ź�
        /// </summary>
        public EventWaitHandle SendMutex { get; set; }

        /// <summary>
        ///     ���յ��ź�
        /// </summary>
        public EventWaitHandle ReceiveMutex { get; set; }
    }
}