using System;
using System.Threading;
using System.Windows.Forms;
using lib.audio;
using lib.metadata;
using player;

namespace librespot
{
    partial class TestForm : Form, Player.IEventsListener
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Button btnPrevious;
        private System.Windows.Forms.Button btnPlayPause;
        private System.Windows.Forms.Button btnNext;
        private System.Windows.Forms.PictureBox pictureBox;
        private System.Windows.Forms.TrackBar trackBarTime;
        private System.Windows.Forms.TrackBar trackBarVolume;

        private Player player;
        private bool playbackPaused = false;
        
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        public delegate void RunThis();

        public TestForm(Player _player, RunThis runThis) 
        {
            this.player = _player;
            InitializeComponent();
            
            player.AddEventsListener(this);

            runThis();
        }

        protected override void OnShown(EventArgs e)
        {
            new Thread(() =>
            {
                while (true)
                {
                    if (player.CurrentPlayable() != null)
                    {
                        trackBarTime.Invoke((Action)(() =>
                        {
                            if (playbackPaused) return;
                            if (player.Time() == -1) return;
                            trackBarTime.Value = player.Time();
                        }));
                    }
                    Thread.Sleep(1000);
                }
            }).Start();
            
            trackBarVolume.Invoke((Action)(() =>
            {
                trackBarVolume.Minimum = 0;
                trackBarVolume.Maximum = 65536;
                trackBarVolume.Value = 65536;
                player.SetVolume(65536);
            }));
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.btnPrevious = new System.Windows.Forms.Button();
            this.btnPlayPause = new System.Windows.Forms.Button();
            this.btnNext = new System.Windows.Forms.Button();
            this.pictureBox = new System.Windows.Forms.PictureBox();
            this.trackBarTime = new System.Windows.Forms.TrackBar();
            this.trackBarVolume = new System.Windows.Forms.TrackBar();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarTime)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarVolume)).BeginInit();
            this.SuspendLayout();
            // 
            // btnPrevious
            // 
            this.btnPrevious.Location = new System.Drawing.Point(200, 380);
            this.btnPrevious.Name = "btnPrevious";
            this.btnPrevious.Size = new System.Drawing.Size(75, 40);
            this.btnPrevious.TabIndex = 0;
            this.btnPrevious.Text = "Previous";
            this.btnPrevious.UseVisualStyleBackColor = true;
            this.btnPrevious.Click += (sender, e) => 
            {
                player.Previous(false);
            };
            // 
            // btnPlayPause
            // 
            this.btnPlayPause.Location = new System.Drawing.Point(300, 380);
            this.btnPlayPause.Name = "btnPlayPause";
            this.btnPlayPause.Size = new System.Drawing.Size(75, 40);
            this.btnPlayPause.TabIndex = 1;
            this.btnPlayPause.Text = "Play/Pause";
            this.btnPlayPause.UseVisualStyleBackColor = true;
            this.btnPlayPause.Click += (sender, e) =>
            {
                player.PlayPause();
            };
            // 
            // btnNext
            // 
            this.btnNext.Location = new System.Drawing.Point(400, 380);
            this.btnNext.Name = "btnNext";
            this.btnNext.Size = new System.Drawing.Size(75, 40);
            this.btnNext.TabIndex = 2;
            this.btnNext.Text = "Next";
            this.btnNext.UseVisualStyleBackColor = true;
            this.btnNext.Click += (sender, e) =>
            {
                player.Next();
            };
            // 
            // pictureBox
            // 
            this.pictureBox.Location = new System.Drawing.Point(250, 30);
            this.pictureBox.Name = "pictureBox";
            this.pictureBox.Size = new System.Drawing.Size(300, 200);
            this.pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox.TabIndex = 3;
            this.pictureBox.TabStop = false;
            // 
            // trackBarTime
            // 
            this.trackBarTime.Location = new System.Drawing.Point(100, 250);
            this.trackBarTime.Name = "trackBarTime";
            this.trackBarTime.Size = new System.Drawing.Size(600, 45);
            this.trackBarTime.TabIndex = 4;
            this.trackBarTime.ValueChanged += (sender, e) =>
            {
                if (trackBarTime.Value >= trackBarTime.Minimum && trackBarTime.Value <= trackBarTime.Maximum)
                {
                    if (!playbackPaused) return;
                    player.Seek(trackBarTime.Value);
                }
            };
            // 
            // trackBarVolume
            // 
            this.trackBarVolume.Location = new System.Drawing.Point(700, 380);
            this.trackBarVolume.Maximum = 65536;
            this.trackBarVolume.Name = "trackBarVolume";
            this.trackBarVolume.Size = new System.Drawing.Size(80, 45);
            this.trackBarVolume.TabIndex = 5;
            this.trackBarVolume.TickStyle = System.Windows.Forms.TickStyle.None;
            this.trackBarVolume.Minimum = 0;
            this.trackBarVolume.ValueChanged += (sender, e) =>
            {
                player.SetVolume(trackBarVolume.Value);
            };
            // 
            // Form1
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.btnPrevious);
            this.Controls.Add(this.btnPlayPause);
            this.Controls.Add(this.btnNext);
            this.Controls.Add(this.pictureBox);
            this.Controls.Add(this.trackBarTime);
            this.Controls.Add(this.trackBarVolume);
            this.Text = "Media Player Skeleton";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarTime)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBarVolume)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        public void OnContextChanged(Player player, string newUri)
        {
        }

        public void OnTrackChanged(Player player, IPlayableId id, MetadataWrapper metadata, bool userInitiated)
        {
        }

        public void OnPlaybackEnded(Player player)
        {
        }

        public void OnPlaybackPaused(Player player, long trackTime)
        {
            playbackPaused = true;
        }

        public void OnPlaybackResumed(Player player, long trackTime)
        {
            playbackPaused = false;
        }

        public void OnPlaybackFailed(Player player, Exception ex)
        {
            playbackPaused = true;
        }

        public void OnTrackSeeked(Player player, long trackTime)
        {
        }

        public void OnMetadataAvailable(Player player, MetadataWrapper metadata)
        {
            trackBarTime.Minimum = 0;
            trackBarTime.Maximum = metadata._track.Duration;
        }

        public void OnPlaybackHaltStateChanged(Player player, bool halted, long trackTime)
        {
        }

        public void OnInactiveSession(Player player, bool timeout)
        {
        }

        public void OnVolumeChanged(Player player, float volume)
        {
        }

        public void OnPanicState(Player player)
        {
            playbackPaused = true;
        }

        public void OnStartedLoading(Player player)
        {
        }

        public void OnFinishedLoading(Player player)
        {
        }
    }
}