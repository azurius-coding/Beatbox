﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Beatbox
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
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

        private static int availablePoints = 0;
        private static int overdraft = 0;
        private static int sumDamage = 0;

        private static double convertRatioAP = 1.2;    // increases the damage
        private static double convertRatioCR = 1.2;     // increases crit chance
        private static double convertRatioHR = 1.05;     // decreases attack rate

        private static BackgroundWorker worker;

        private Storyboard circleStoryboard;
        private Storyboard explosionStoryboard;
        private DoubleAnimation rotateAnimation;
        private DoubleAnimation opacityAnimation;
        private DoubleAnimation sizeAnimation;

        private static int animationMaxFontSize = 30;
        private static bool isChangingRate = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Executes after window is rendered. It is required to update the progress bar.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            InitWorker();
            InitCircleAnimation();
            InitExplosionAnimation();
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
        private void InitCircleAnimation()
        {
            rotateAnimation = new DoubleAnimation();
            circleStoryboard = new Storyboard();

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

        /// <summary>
        /// Method for the BackgroundWorker tasks.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Doing work");
            int dmg = 0;
            currentXP = Math.Max(0, overdraft);
            while(currentXP < maxDamageValueForLevel)
            {
                // if cancelation is needed, see "completed" method
                // call worker.CancelAsync() from UI to set CancellationPending
                if ((sender as BackgroundWorker).CancellationPending == true)
                {
                    e.Cancel = true;
                    return;
                }
                dmg = CalcDamageValue();
                currentXP += dmg;
                sumDamage += dmg;
                System.Diagnostics.Debug.WriteLine("dmg: {0}, sum: {1}", dmg, currentXP);
                overdraft = currentXP - maxDamageValueForLevel;
                // must be a percentage
                int percentage = (int)(100 * currentXP / (double)maxDamageValueForLevel);
                (sender as BackgroundWorker).ReportProgress(percentage, dmg);

                Thread.Sleep(currentAttackRate);
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
            FireExplosionEvent((int)e.UserState, (int)e.UserState > currentDamagePerHit);
            UpdateDPS();
            UpdateRecordDamage((int)e.UserState);
            AppendToLog((int)e.UserState, "\n");
            CurrentDamageDoneValue.Content = sumDamage;
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


        private void RotateAnimation_Completed(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Storyboard completed.");
            if (isChangingRate)
            {
                UpdateAnimation(currentAttackRate);
                isChangingRate = false;
            }
            circleStoryboard.Begin();
        }

        private int CalcDamageValue()
        {
            Random random = new Random();

            int minDmg = (int)(currentDamagePerHit * 0.75);
            int returned = random.Next(minDmg, currentDamagePerHit + 1);

            currentCritChance = baseCritChance * Math.Pow(convertRatioCR, currentCR - 1);
            bool check = random.NextDouble() * 100 + 1 <= currentCritChance;
            if (check)
            {
                returned *= 2;
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

        private void UpdateAnimation(int duration)
        {
            circleStoryboard.Duration = new Duration(TimeSpan.FromMilliseconds(duration));
            rotateAnimation.Duration = circleStoryboard.Duration;
        }

        /// <summary>
        /// Updates attack rate with a given formula.
        /// </summary>
        private void UpdateAttackRate()
        {
            currentAttackRate = (int) (baseAttackRate / Math.Pow(convertRatioHR, currentHR - 1));
            AttackRateValue.Content = currentAttackRate / 1000.0;
            isChangingRate = true;
        }

        /// <summary>
        /// Updates constraints for min and max damage for the level-up.
        /// </summary>
        private void UpdateLevelConstraints()
        {
            currentLevel += 1;
            maxDamageValueForLevel = maxDamageValueForLevel * currentLevel;
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
                currentCritChance * currentDamagePerHit * 1.75 / (2.0 * currentAttackRate / 1000);
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
            CritChanceValue.Content = currentCritChance;
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
                AppendToLog("Starting to hit stuff...", "\n");
            }
        }

        private void StopBeatbox(object sender, RoutedEventArgs e)
        {
            if (worker.IsBusy)
            {
                worker.CancelAsync();
                circleStoryboard.Stop();
                AppendToLog("Enough hitting, going to stop now.", "\n");
            }
        }
    }
}
