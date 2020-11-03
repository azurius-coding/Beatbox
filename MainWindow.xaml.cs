﻿using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Xml.Serialization;

namespace Beatbox
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        private static string info = "Current version: 0.0.1-pre-release";
        private static int maxDamageValueForLevel = 100;

        private static int damagePerHit = 10;
        private static int baseAttackRate = 2500;       // in millisec
        private static double baseCritChance = 5.0;

        private static int currentXP = 0;
        private static int currentLevel = 0;
        private static int currentAP = 1;
        private static int currentCR = 1;
        private static int currentHR = 1;
        private static int currentDamagePerHit = damagePerHit;
        private static int currentAttackRate = baseAttackRate;
        private static double currentCritChance = 5.0;
        private static int currentRecord = 0;
        private static int currentHit = 0;

        private static int availablePoints = 0;
        private static int overdraft = 0;
        private static int sumDamage = 0;

        private static int attackRateOffset = 200;
        private static System.Timers.Timer timer;
        private static bool isUserInputCrit = false;
        private static bool isKeyDown = false;
        private static int timerCount = 0;
        private static int timerInterval = 100;

        // conversion factors
        private static double convertRatioAP = 1.2;    // increases the damage
        private static double convertRatioCR = 1.2;     // increases crit chance
        private static double convertRatioHR = 1.05;     // decreases attack rate
        private static double convertRatioXP = 1.05;     // decreases xp

        // calculation
        private static BackgroundWorker worker;

        // animation
        private static Storyboard circleStoryboard;
        private static Storyboard explosionStoryboard;
        private static Storyboard critMessageStoryboard;
        private static DoubleAnimation rotateAnimation;
        private static DoubleAnimation opacityAnimation;
        private static DoubleAnimation sizeAnimation;
        private static DoubleAnimation critMessageAnimation;

        private static int animationMaxFontSize = 30;
        private static bool isChangingRate = false;
        private static bool isWorkerUnpaused = false;
        private static bool isWorkerProgressChanged = false;
        private static bool isRotateAnimationCompleted = false;

        // for resize listener
        const int WM_SIZING = 0x214;
        const int WM_EXITSIZEMOVE = 0x232;
        private static bool WindowWasResized = false;
        private double initFrameWidth;
        private double initFrameHeight;

        // Global HotKeys
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID = 9000;

        //Modifiers:
        private const uint MOD_NONE = 0x0000; //[NONE]
        private const uint MOD_ALT = 0x0001; //ALT
        private const uint MOD_CONTROL = 0x0002; //CTRL
        private const uint MOD_SHIFT = 0x0004; //SHIFT
        private const uint MOD_WIN = 0x0008; //WINDOWS
                                             
        private const uint VK_CAPITAL = 0x14; //CAPS LOCK:
        private const uint VK_SPACE = 0x20; //SPACE BAR

        private HwndSource source;

        public MainWindow()
        {
            InitializeComponent();
            //this.Loaded += MainWindow_Loaded;
            timer = new System.Timers.Timer(timerInterval);
            timer.Elapsed += TimedEvent;
        }

        private void StopButton_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }

        private void StartButton_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }

        /// <summary>
        /// Executes after window is rendered. It is required to update the progress bar.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            InitWorker();
            InitRotateAnimation();
            InitExplosionAnimation();
            InitCritMessageAnimation();
            HideIncreaseButtons();
            // init UI
            ValueAP.Content = currentAP;
            ValueCR.Content = currentCR;
            ValueHR.Content = currentHR;
            CurrentDamageValue.Content = "7 - 10";
            CritChanceValue.Content = currentCritChance;
            AttackRateValue.Content = currentAttackRate/1000.0;
            LevelValue.Content = 0;
            CurrentDamageDoneValue.Content = 0;
            NextLevelAtValue.Content = 100;
            CritLabel.Text = "Crit!";
            CritLabel.Opacity = 0.0;

            initFrameHeight = ContentFrame.ActualHeight;
            initFrameWidth = ContentFrame.ActualWidth;
        }

        private void InitWorker()
        {
            System.Diagnostics.Debug.WriteLine("Worker started...");
            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
        }

        /// <summary>
        /// Initializes the animation of the image with starting values.
        /// Update the duration later using
        /// <code>rotateAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(baseAttackRate));</code>
        /// </summary>
        private void InitRotateAnimation()
        {
            rotateAnimation = new DoubleAnimation();
            circleStoryboard = new Storyboard();
            circleStoryboard.Completed += new EventHandler(CircleStoryboard_Completed);

            rotateAnimation.From = 0;
            rotateAnimation.To = 360;
            rotateAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(baseAttackRate));
            rotateAnimation.Completed += new EventHandler(RotateAnimation_Completed);

            Storyboard.SetTarget(rotateAnimation, rotatingArrow);
            Storyboard.SetTargetProperty(rotateAnimation, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            circleStoryboard.Children.Add(rotateAnimation);
            circleStoryboard.Duration = new Duration(TimeSpan.FromMilliseconds(baseAttackRate));
            //circleStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            //circleStoryboard.Completed += new EventHandler(CircleStoryboard_Completed);

            rotatingArrow.RenderTransform = new RotateTransform();

            Resources.Add("Storyboard", circleStoryboard);
        }

        private void InitExplosionAnimation()
        {
            opacityAnimation = new DoubleAnimation();
            sizeAnimation = new DoubleAnimation();
            explosionStoryboard = new Storyboard();

            opacityAnimation.From = 0.0;
            opacityAnimation.To = 1.0;
            opacityAnimation.AutoReverse = true;
            opacityAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(500));

            sizeAnimation.From = animationMaxFontSize/3;
            sizeAnimation.To = animationMaxFontSize;
            sizeAnimation.AutoReverse = true;
            sizeAnimation.Duration = opacityAnimation.Duration;

            Storyboard.SetTarget(opacityAnimation, ExplosionLabel);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(TextBlock.OpacityProperty));
            Storyboard.SetTarget(sizeAnimation, ExplosionLabel);
            Storyboard.SetTargetProperty(sizeAnimation, new PropertyPath(TextBlock.FontSizeProperty));

            explosionStoryboard.Children.Add(opacityAnimation);
            explosionStoryboard.Children.Add(sizeAnimation);
            explosionStoryboard.Duration = opacityAnimation.Duration;
            explosionStoryboard.AutoReverse = true;
            //explosionStoryboard.Completed += new EventHandler(ExplosionStoryboard_Completed);

            Resources.Add("explosionStoryboard", explosionStoryboard);
        }

        private void InitCritMessageAnimation()
        {
            critMessageAnimation = new DoubleAnimation();
            critMessageStoryboard = new Storyboard();

            critMessageAnimation.From = 0.0;
            critMessageAnimation.To = 1.0;
            critMessageAnimation.AutoReverse = true;
            critMessageAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(1000));

            critMessageStoryboard.Children.Add(critMessageAnimation);
            critMessageStoryboard.Duration = critMessageAnimation.Duration;
            critMessageStoryboard.AutoReverse = critMessageAnimation.AutoReverse;

            Storyboard.SetTarget(critMessageAnimation, CritLabel);
            Storyboard.SetTargetProperty(critMessageAnimation, new PropertyPath(TextBlock.OpacityProperty));
        }

        /// <summary>
        /// Method for the BackgroundWorker tasks.
        /// It calculates the current damage from the hit by asking the <code>CalcDamageValue()</code> method,
        /// then updates the values for required UI labels and further calculations.
        /// When this is done, the worker has to wait until the rotation animation is completed before
        /// using the calculated values to update the UI.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Doing work");
            //currentXP = Math.Max(0, overdraft);
            while (true)
            {
                // if cancelation is needed, see "completed" method
                // call worker.CancelAsync() from UI to set CancellationPending
                if ((sender as BackgroundWorker).CancellationPending == true)
                {
                    e.Cancel = true;
                    return;
                }
                this.Dispatcher.Invoke(new Action(() => isWorkerProgressChanged = false));

                currentHit = CalcDamageValue();
                currentXP += currentHit;
                sumDamage += currentHit;
                System.Diagnostics.Debug.WriteLine("dmg: {0}, sum: {1}", currentHit, currentXP);
                overdraft = currentXP - maxDamageValueForLevel;

                // must be a percentage
                int percentage = (int)(100 * currentXP / (double)maxDamageValueForLevel);

                (sender as BackgroundWorker).ReportProgress(percentage, currentHit);

                Thread.Sleep(currentAttackRate);
                // use System.Timers.Timer maybe

                if (overdraft >= 0)
                {
                    currentXP = overdraft;
                    return;
                }
            }
        }

        /// <summary>
        /// Used to reflect any changes that happen due to the BackgroundWorker.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("progress changed");
            
            XPBar.Value = e.ProgressPercentage;

            CurrentDamageDoneValue.Content = e.ProgressPercentage * maxDamageValueForLevel / 100;

            isWorkerProgressChanged = true;
        }

        /// <summary>
        /// Executed when the BackgroundWorker finished.
        /// When it finishes due to user input or an error, it disposes resources.
        /// When finishing normally, it hides buttons and updates level constraints and progress bar.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Worker completed.");
            
            if (e.Cancelled)    // manual cancellation
            {
                //worker.CancelAsync();
                (sender as BackgroundWorker).Dispose();
            }
            else if (e.Error != null)   // error occured
            {
                //worker.CancelAsync();
                (sender as BackgroundWorker).Dispose();
            }
            else    // normal continuation
            {
                // update calculations and UI
                ShowIncreaseButtons();
                UpdateLevelConstraints();
                UpdateProgressBar();

                availablePoints++;

                if (overdraft > 0)
                {
                    currentXP = overdraft;
                }
                else
                {
                    currentXP = 0;
                }

                (sender as BackgroundWorker).RunWorkerAsync();
            }
        }

        /// <summary>
        /// This method is called after every iteration of the DoubleAnimation.
        /// It is used to synchronize showing of damage values and updating log, since
        /// it is the main indicator for the user to see when next attacks occur.
        /// The XP bar is still being updated by the background worker directly.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RotateAnimation_Completed(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Storyboard completed.");
            if (isChangingRate)
            {
                UpdateRotateAnimation(currentAttackRate);
                isChangingRate = false;
            }

            UpdateDPS();
            UpdateRecordDamage(currentHit);

            bool check = currentHit > currentDamagePerHit;
            string separator = check ? " (critical strike)\n" : "\n";
            AppendToLog(currentHit, separator);

            FireExplosionEvent(currentHit, currentHit > currentDamagePerHit);

            isKeyDown = false;

            timer.Start();
            isRotateAnimationCompleted = true;
        }

        private void CircleStoryboard_Completed(object sender, EventArgs e)
        {
            if (!worker.IsBusy)
            {
                worker.RunWorkerAsync();
            }
            isRotateAnimationCompleted = false;
            circleStoryboard.Begin();
        }

        private int CalcDamageValue()
        {
            Random random = new Random();

            int minDmg = (int)(currentDamagePerHit * 0.75);
            int returned = random.Next(minDmg, currentDamagePerHit + 1);

            currentCritChance = baseCritChance * Math.Pow(convertRatioCR, currentCR - 1);
            bool check = random.NextDouble() * 100 + 1 <= currentCritChance;
            if (check || isUserInputCrit)
            {
                returned *= 2;
                isUserInputCrit = false;
            }

            return returned;
        }

        /// <summary>
        /// Function to animate label on the UI, that shows damage values
        /// inside the circle as an explosion, by adjusting fontsize and opacity.
        /// If the value is a critical strike, shows extra effects.
        /// </summary>
        /// <param name="damageValue"></param>
        /// <param name="isCrit"></param>
        private void FireExplosionEvent(int damageValue, bool isCrit)
        {
            ExplosionLabel.Text = Convert.ToString(damageValue);
            if (isCrit)
            {
                ExplosionLabel.FontWeight = FontWeights.Bold;
                ExplosionLabel.FontStyle = FontStyles.Italic;
                sizeAnimation.To = sizeAnimation.To * 1.5;
            }
            else
            {
                ExplosionLabel.FontWeight = FontWeights.Normal;
                ExplosionLabel.FontStyle = FontStyles.Normal;
                sizeAnimation.To = animationMaxFontSize;
            }
            explosionStoryboard.Begin();
        }

        private void HandleCritMessage(int offset)
        {
            if (offset < 0)
            {
                return;
            }
            else if (offset <= attackRateOffset)
            {
                CritLabel.Text = "Perfect!";
                isUserInputCrit = true;
            }
            else if (offset <= attackRateOffset * 2)
            {
                CritLabel.Text = "Close";
            }
            else
            {
                CritLabel.Text = "Missed";
            }
            critMessageStoryboard.Begin();
        }

        private void UpdateRotateAnimation(int durationInMilliSec)
        {
            circleStoryboard.Duration = new Duration(TimeSpan.FromMilliseconds(durationInMilliSec));
            rotateAnimation.Duration = circleStoryboard.Duration;
        }

        /// <summary>
        /// Updates attack rate with a given formula.
        /// </summary>
        private void UpdateAttackRate()
        {
            currentAttackRate = (int) (baseAttackRate / Math.Pow(convertRatioHR, currentHR - 1));
            AttackRateValue.Content = Math.Round(currentAttackRate / 1000.0,2);
            isChangingRate = true;
        }

        /// <summary>
        /// Updates constraints for min and max damage for the level-up.
        /// </summary>
        private void UpdateLevelConstraints()
        {
            currentLevel += 1;
            maxDamageValueForLevel = (int)(maxDamageValueForLevel * Math.Pow(convertRatioXP,currentLevel));
            LevelValue.Content = currentLevel;
            NextLevelAtValue.Content = maxDamageValueForLevel;
        }

        /// <summary>
        /// Updates minimum and maximum damage possible.
        /// </summary>
        private void UpdateDamagePerHit()
        {
            currentDamagePerHit = (int) (currentDamagePerHit * convertRatioAP);
            StringBuilder sb = new StringBuilder();
            sb.Append((int)(currentDamagePerHit * 0.75));
            sb.Append(" - ");
            sb.Append(currentDamagePerHit);
            CurrentDamageValue.Content = sb.ToString();
            System.Diagnostics.Debug.WriteLine("currentMinDamage: {0}", (int)(currentDamagePerHit * 0.75));
            System.Diagnostics.Debug.WriteLine("currentMaxDamage: {0}", (int)(currentDamagePerHit * 0.75));
        }

        /// <summary>
        /// Used to update the progress bar on the UI based on new data.
        /// Invoke only after maxDamageValueForLevel had been updated.
        /// </summary>
        private void UpdateProgressBar()
        {
            XPBar.Value = (int)(100*currentXP/(double)maxDamageValueForLevel);
        }

        /// <summary>
        /// Function to update the damage per second. It is calculated entirely on average damage
        /// including critical hit chance.
        /// </summary>
        private void UpdateDPS()
        {
            DamagePerSecondValue.Content =
                Math.Round(currentCritChance * currentDamagePerHit * 1.75 / (2.0 * currentAttackRate / 1000),2);
        }

        /// <summary>
        /// Checks if the given value for damage is higher than the current record and updates accordingly.
        /// </summary>
        /// <param name="dmg">Value to be compared with current record.</param>
        private void UpdateRecordDamage(int dmg)
        {
            if (dmg > currentRecord)
            {
                currentRecord = dmg;
                RecordDamageValue.Content = currentRecord;
            }
        }

        private void UpdateCritChance()
        {
            CritChanceValue.Content = Math.Round(currentCritChance, 2);
        }

        /// <summary>
        /// Event handler to increase attack power.
        /// </summary>
        /// <param name="sender">Button object that fired the event.</param>
        /// <param name="e"></param>
        private void IncreaseAP(object sender, RoutedEventArgs e)
        {
            if (availablePoints > 0)
            {
                currentAP++;
                availablePoints--;
                ValueAP.Content = currentAP;
                UpdateDamagePerHit();
                if (availablePoints == 0)
                {
                    HideIncreaseButtons();
                }
                AppendToLog("Attack Power upgraded by 1.", "\n");
            }
            
        }

        /// <summary>
        /// Event handler to increase critical rating.
        /// </summary>
        /// <param name="sender">Button object that fired the event.</param>
        /// <param name="e"></param>
        private void IncreaseCR(object sender, RoutedEventArgs e)
        {
            if (availablePoints > 0)
            {
                currentCR++;
                availablePoints--;
                ValueCR.Content = currentCR;
                UpdateCritChance();
                if (availablePoints == 0)
                {
                    HideIncreaseButtons();
                }
                AppendToLog("Critical Strike Rating upgraded by 1.", "\n");
            }
        }

        /// <summary>
        /// Event handler to increase haste rating.
        /// </summary>
        /// <param name="sender">Button object that fired the event.</param>
        /// <param name="e"></param>
        private void IncreaseHR(object sender, RoutedEventArgs e)
        {
            if (availablePoints > 0)
            {
                currentHR++;
                availablePoints--;
                ValueHR.Content = currentHR;
                UpdateAttackRate();
                if (availablePoints == 0)
                {
                    HideIncreaseButtons();
                }
                AppendToLog("Haste Rating upgraded by 1.", "\n");
            }
        }

        private void HideIncreaseButtons()
        {
                IncrBttnAP.Visibility = Visibility.Hidden;
                IncrBttnCR.Visibility = Visibility.Hidden;
                IncrBttnHR.Visibility = Visibility.Hidden;
                IncrBttnAP.IsEnabled = false;
                IncrBttnCR.IsEnabled = false;
                IncrBttnHR.IsEnabled = false;
        }

        private void ShowIncreaseButtons()
        {
            IncrBttnAP.Visibility = Visibility.Visible;
            IncrBttnCR.Visibility = Visibility.Visible;
            IncrBttnHR.Visibility = Visibility.Visible;
            IncrBttnAP.IsEnabled = true;
            IncrBttnCR.IsEnabled = true;
            IncrBttnHR.IsEnabled = true;
        }

        private void AppendToLog(Object text, String seperator)
        {
            StringBuilder sb = new StringBuilder(Log.Text);
            sb.Append(text);
            sb.Append(seperator);
            Log.Text = sb.ToString();
            ScrollViewer.ScrollToEnd();
        }

        private void StartBeatbox(object sender, RoutedEventArgs e)
        {
            if (!worker.IsBusy)
            {
                worker.RunWorkerAsync();
                circleStoryboard.Begin();
                timer.Start();
                AppendToLog("Starting to hit stuff...", "\n");
            }
        }

        private void StopBeatbox(object sender, RoutedEventArgs e)
        {
            if (worker.IsBusy)
            {
                circleStoryboard.Stop();
                worker.CancelAsync();
                timer.Stop();
                AppendToLog("Enough hitting, going to stop now.", "\n");
            }
        }

        /// <summary>
        /// Save object data into XML file.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filePath"></param>
        /// <param name="objectToWrite"></param>
        /// <param name="append"></param>
        private static void WriteToXmlFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            TextWriter writer = null;
            try
            {
                var serializer = new XmlSerializer(typeof(T));
                writer = new StreamWriter(filePath, append);
                serializer.Serialize(writer, objectToWrite);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }

        /// <summary>
        /// Load data from XML file to restore previous game.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static T ReadFromXmlFile<T>(string filePath) where T : new()
        {
            TextReader reader = null;
            try
            {
                var serializer = new XmlSerializer(typeof(T));
                reader = new StreamReader(filePath);
                return (T)serializer.Deserialize(reader);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        private void Menu_New_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(Process.GetCurrentProcess().MainModule.FileName);
            Application.Current.Shutdown();
        }

        private void Menu_Open_Click(object sender, RoutedEventArgs e)
        {
            string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            path += "/beatbox.xml";
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.FileName = "beatbox_save";
            dlg.DefaultExt = ".xml";

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Save document
                path = dlg.FileName;
            }

            BeatboxSave save = ReadFromXmlFile<BeatboxSave>(path);
            currentLevel = save.currentLevel;
            currentXP = save.currentXP;
            currentRecord = save.currentRecord;
            availablePoints = save.availablePoints;
            currentAP = save.currentAP;
            currentCR = save.currentCR;
            currentHR = save.currentHR;
            currentCritChance = save.currentCritChance;
            currentDamagePerHit = save.currentDamagePerHit;
            currentAttackRate = save.currentAttackRate;
            overdraft = save.overdraft;
            sumDamage = save.sumDamage;
        }

        private void Menu_Save_Click(object sender, RoutedEventArgs e)
        {
            string path = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            path += "/beatbox.xml";
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.FileName = "beatbox_save";
            dlg.DefaultExt = ".xml";

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Save document
                path = dlg.FileName;
            }

            BeatboxSave save = BeatboxSave.Instance;
            save.currentLevel = currentLevel;
            save.currentXP = currentXP;
            save.currentRecord = currentRecord;
            save.availablePoints = availablePoints;
            save.currentAP = currentAP;
            save.currentCR = currentCR;
            save.currentHR = currentHR;
            save.currentCritChance = currentCritChance;
            save.currentDamagePerHit = currentDamagePerHit;
            save.currentAttackRate = currentAttackRate;
            save.overdraft = overdraft;
            save.sumDamage = sumDamage;

            WriteToXmlFile(path, save);
        }

        private void Menu_Exit_Click(object sender, RoutedEventArgs e)
        {
            worker.CancelAsync();
            circleStoryboard.Stop();
            timer.Stop();
            timer.Dispose();
            explosionStoryboard.Stop();
            worker.Dispose();
            Environment.Exit(1);
        }

        private void Menu_Reset_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxButton button = MessageBoxButton.YesNo;
            MessageBoxResult result = MessageBox.Show("Are you sure to reset all talent points?", "Reset Points", button);
            if (result.Equals(MessageBoxResult.Yes))
            {
                worker.CancelAsync();
                circleStoryboard.Stop();
                timer.Stop();
                timer.Dispose();
                explosionStoryboard.Stop();
                // make sure worker is finished before reset
                while (worker.IsBusy)
                {
                    Thread.Sleep(100);
                }
                currentAP = 1;
                currentCR = 1;
                currentHR = 1;
                currentDamagePerHit = damagePerHit;
                currentAttackRate = baseAttackRate;
                currentCritChance = baseCritChance;

                availablePoints = currentLevel;
                if (currentLevel > 0)
                {
                    ShowIncreaseButtons();
                }
            }
            else
            {
                return;
            }
        }

        private void Menu_Info_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxButton button = MessageBoxButton.OK;
            MessageBox.Show(info, "About this application", button);
        }

        private void TimedEvent(object sender, ElapsedEventArgs e)
        {
            if (isKeyDown)
            {
                timer.Stop();
                int check = currentAttackRate - timerCount * timerInterval;
                System.Diagnostics.Debug.WriteLine("Check {0}", check);
                this.Dispatcher.BeginInvoke(new Action(() => HandleCritMessage(check)));
                timerCount = 0;
            }
            else
            {
                timerCount++;
            }
        }

        private void ContentFrame_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.IsRepeat || isKeyDown)
            {
                return;
            }
            if (e.Key == Key.Space)
            {
                System.Diagnostics.Debug.WriteLine("Space key pressed.");
                isKeyDown = true;
            }
        }

        // global HotKey
        private IntPtr _windowHandle;
        private HwndSource _source;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _windowHandle = new WindowInteropHelper(this).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source.AddHook(HwndHook);

            //RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_CONTROL, VK_CAPITAL); //CTRL + CAPS_LOCK
            RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_NONE, VK_SPACE);
        }

        protected override void OnClosed(EventArgs e)
        {
            _source.RemoveHook(HwndHook);
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            base.OnClosed(e);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            switch (msg)
            {
                case WM_HOTKEY:
                    switch (wParam.ToInt32())
                    {
                        case HOTKEY_ID:
                            int vkey = (((int)lParam >> 16) & 0xFFFF);
                            if (vkey == VK_SPACE)
                            {
                                //handle global hot key here...
                                isKeyDown = true;
                            }
                            handled = true;
                            break;
                    }
                    break;
            }
            return IntPtr.Zero;
        }
    }
}
