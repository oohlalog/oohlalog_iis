using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace OohLaLog
{
    public class LogBuffer
    {
        public static int DefaultBufferLimit = 150;
        public static double DefaultBufferInterval = 60; // 60 seconds
        private bool m_closing = false;
        private List<string> m_items = new List<string>();
        public LogBuffer(IISAppender appender)
        {
            Appender = appender;
            BufferLimit = DefaultBufferLimit;
            BufferInterval = DefaultBufferInterval;
        }
        public int BufferLimit { get; set; }
        public double BufferInterval { get; set; } //0 disables buffer
        public bool BufferEnabled { get; set; }
        private Timer BufferTimer { get; set; }
        private IISAppender Appender { get; set; }

        public void ActivateOptions()
        {
            if (BufferInterval > 0)
            {
                BufferEnabled = true;
                if (BufferInterval < 1) BufferInterval = 1; //1 second minimum
                if (BufferLimit < 1) BufferLimit = 1; //1 minimum but very inefficient
                BufferTimer = new System.Timers.Timer(BufferInterval * 1000);
                BufferTimer.Elapsed += buffertimer_Elapsed;
            }
            if (BufferTimer != null) StartBufferTimer();
        }
        private void buffertimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            FlushBuffer();
        }
        private void StopBufferTimer()
        {
            if (BufferTimer != null)
                BufferTimer.Stop();
        }
        private void StartBufferTimer()
        {
            if (BufferTimer != null && !m_closing) BufferTimer.Start();
        }
        private void FlushBuffer()
        {
            if (m_items.Count == 0) return;
            AddItem(null);
        }
        public void Close()
        {
            lock (this)
            {
                m_closing = true;
                StopBufferTimer();
                FlushBuffer();
            }
        }
        public void AddItem(string item)
        {
            string[] items = null;
            bool flushBuffer = false;
            lock (this)
            {
                if (item != null)
                    m_items.Add(item);
                else
                    flushBuffer = true;
                if (m_items.Count >= BufferLimit || flushBuffer)
                {
                    StopBufferTimer();
                    if (m_items.Count > 0)
                    {
                        items = new string[m_items.Count];
                        m_items.CopyTo(items);
                        m_items.Clear();
                    }
                    StartBufferTimer();
                }
            }
            if (items != null && !m_closing)
                Appender.sendLogs(items);
            else if (items != null)
                Appender.sendLogs(items, false);
        }
    }
}
