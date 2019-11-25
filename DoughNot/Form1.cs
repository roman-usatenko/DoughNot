using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using Emgu.CV;
using System.Windows.Forms;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DoughNot
{
    public partial class Form1 : Form
    {
        private VideoCapture _capture;
        private Mat _frame = new Mat();
        private DateTime _lastRecognitionTime;
        private CascadeClassifier _faceClassifier;
        private bool _progress = false;

        public Form1()
        {
            InitializeComponent();
            CvInvoke.UseOpenCL = false;
            try
            {
                _capture = new VideoCapture();
                _capture.ImageGrabbed += ProcessFrame;
            }
            catch (Exception excpt)
            {
                MessageBox.Show(excpt.Message);
            }
            labelLastDetectionTime.Text = "Capture is paused";
            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
            notifyIcon1.Text = Text;
        }

        private void ProcessFrame(object sender, EventArgs arg)
        {
            int lastDetectionAgo = (int)(DateTime.Now - _lastRecognitionTime).TotalSeconds;
            _progress = lastDetectionAgo > 0 && lastDetectionAgo % 5 == 0;
            if (!_progress)
            {
                return;
            }

            if (_capture != null && _capture.Ptr != IntPtr.Zero)
            {
                _capture.Retrieve(_frame, 0);
                Detect();
                captureImageBox.Image = _frame;
            }
        }

        private void ReleaseData()
        {
            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
            _capture?.Dispose();
            _faceClassifier?.Dispose();
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            if (_capture != null)
            {
                if (timer1.Enabled)
                {
                    PauseCapture();
                }
                else
                {
                    StartCapture();
                }
            }
        }

        private void Detect()
        {
            using (InputArray iaImage = _frame.GetInputArray())
            {
                using (UMat ugray = new UMat())
                {
                    CvInvoke.CvtColor(_frame, ugray, Emgu.CV.CvEnum.ColorConversion.Bgr2Gray);
                    CvInvoke.EqualizeHist(ugray, ugray);
                    Rectangle[] facesDetected = _faceClassifier.DetectMultiScale(
                       ugray,
                       1.1,
                       10,
                       new Size(20, 20));

                    if (facesDetected?.Length > 0)
                    {
                        CvInvoke.Rectangle(_frame, facesDetected[0], new Bgr(Color.Red).MCvScalar, 2);
                        _lastRecognitionTime = DateTime.Now;
                    }
                }
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            TimeSpan timeSpan = DateTime.Now - _lastRecognitionTime;
            labelLastDetectionTime.Text = "Face detected " + Math.Round(timeSpan.TotalSeconds) + " seconds ago";

            labelProgress.Text = _progress ? "*" : " ";

            if (timeSpan.TotalSeconds >= Properties.Settings.Default.DetectionTimeout)
            {
                LockWorkStation();
            }
        }

        [DllImport("user32")]
        public static extern void LockWorkStation();

        private void PauseCapture()
        {
            buttonStart.Text = "Resume";
            labelLastDetectionTime.Text = "Capture is paused";
            _capture.Pause();
            timer1.Enabled = false;
            _progress = false;
        }

        private void StartCapture()
        {
            _lastRecognitionTime = DateTime.Now;
            buttonStart.Text = "Pause";
            labelLastDetectionTime.Text = "Detecting face...";
            _capture.Start();
            timer1.Enabled = true;
        }

        public void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock)
            {
                PauseCapture();
            }
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                StartCapture();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            textBoxTimeout.Text = "" + Properties.Settings.Default.DetectionTimeout;
            textBoxDetection.SelectedItem = Properties.Settings.Default.DetectionOption;
            _faceClassifier = new CascadeClassifier(Application.StartupPath + "\\haarcascade_frontalface_" + textBoxDetection.SelectedItem.ToString().ToLower() + ".xml");
            StartCapture();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                notifyIcon1.Visible = true;
            }
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            notifyIcon1.Visible = false;
        }

        private bool IsTimeoutValid(out int result)
        {
            return int.TryParse(textBoxTimeout.Text, out result)
                && result >= 10
                && result <= 600;
        }

        private void textBoxTimeout_TextChanged(object sender, EventArgs e)
        {
            if (!IsTimeoutValid(out int result))
            {
                textBoxTimeout.BackColor = Color.LightPink;
            }
            else
            {
                textBoxTimeout.BackColor = SystemColors.Window;
                Properties.Settings.Default.DetectionTimeout = result;
                Properties.Settings.Default.Save();
            }
        }

        private void textBoxDetection_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                CascadeClassifier newFaceClassifier = new CascadeClassifier(Application.StartupPath + "\\haarcascade_frontalface_" + textBoxDetection.SelectedItem.ToString().ToLower() + ".xml");
                _faceClassifier?.Dispose();
                _faceClassifier = newFaceClassifier;
                Properties.Settings.Default.DetectionOption = textBoxDetection.SelectedItem.ToString();
                Properties.Settings.Default.Save();
            }
            catch (Exception excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }

        private void textBoxTimeout_Leave(object sender, EventArgs e)
        {
            if (!IsTimeoutValid(out int result))
            {
                textBoxTimeout.Text = "" + Properties.Settings.Default.DetectionTimeout;
            }
        }
    }
}
