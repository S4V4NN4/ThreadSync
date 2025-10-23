using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReadersWriters
{
    public partial class MainWindow : Window
    {
        private List<Label> readerLabels = new();
        private List<Label> writerLabels = new();
        private Random rand = new();

        private bool running = false;

        private Semaphore readerSemaphore;
        private SemaphoreSlim mutex = new(1, 1);
        private object lockObj = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        int readers = 0;
        int writers = 0;
        int value = 0;
        int maxReaders = 0;

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(ReadersBox.Text, out int readers) || readers <= 0)
            {
                MessageBox.Show("Введите кол-во читателей (>0)", "Читатели", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(WritersBox.Text, out int writers) || writers <= 0)
            {
                MessageBox.Show("Введите кол-во писателей (>0)", "Писатели", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(MaxReadersBox.Text, out int maxReaders) || maxReaders <= 0)
            {
                MessageBox.Show("Введите макс. кол-во одновременных читателей (>0)", "Одновременные читатели", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Очистка
            ReadersPanel.Items.Clear();
            WritersPanel.Items.Clear();
            readerLabels.Clear();
            writerLabels.Clear();
            QueueBox.Text = "-";
            SharedValueBox.Text = "0";

            readerSemaphore = new Semaphore(maxReaders, maxReaders);
            value = 0;
            running = true;

            for (int i = 0; i < readers; i++)
            {
                var label = CreateLabel("Читатель " + i + 1, Brushes.LightBlue);
                ReadersPanel.Items.Add(label);
                readerLabels.Add(label);

                int id = i;
                new Thread(() => ReaderLoop(id)) { IsBackground = true }.Start();
            }

            for (int i = 0; i < writers; i++)
            {
                var label = CreateLabel("Писатель " + i + 1, Brushes.LightCoral);
                WritersPanel.Items.Add(label);
                writerLabels.Add(label);

                int id = i;
                new Thread(() => WriterLoop(id)) { IsBackground = true }.Start();
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            running = false;
        }

        private Label CreateLabel(string text, Brush color)
        {
            return new Label
            {
                Content = text + ": Ожидание",
                Background = color,
                Margin = new Thickness(3),
                Padding = new Thickness(3),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };
        }

        private void ReaderLoop(int id)
        {
            var label = readerLabels[id];
            while (running)
            {
                Dispatcher.Invoke(() => label.Content = "Читатель " + id + 1 + ": ждёт");
                QueueAdd("R-" + id + 1);

                readerSemaphore.WaitOne();
                QueueRemove("R-" + id + 1);

                Dispatcher.Invoke(() => label.Content = "Читатель " + id + 1 + ": читает");

                lock (lockObj)
                {
                    int value = this.value;
                    Thread.Sleep(rand.Next(2000, 5000));
                }

                readerSemaphore.Release();
                Dispatcher.Invoke(() => label.Content = "Читатель " + id + 1 + ": завершил");

                Thread.Sleep(rand.Next(2000, 5000));
            }
            Dispatcher.Invoke(() => label.Content = "Читатель " + id + 1 + ": остановлен");
        }

        private void WriterLoop(int id)
        {
            var label = writerLabels[id];
            while (running)
            {
                Dispatcher.Invoke(() => label.Content = "Писатель " + id + 1 + ": ждёт");
                QueueAdd("W-" + id + 1);

                mutex.Wait();
                QueueRemove("W-" + id + 1);

                Dispatcher.Invoke(() => label.Content = "Писатель " + id + 1 + ": пишет");

                value = rand.Next(0, 1000);
                Dispatcher.Invoke(() => SharedValueBox.Text = value.ToString());
                Thread.Sleep(rand.Next(3000, 7000));

                mutex.Release();
                Dispatcher.Invoke(() => label.Content = "Писатель " + id + 1 + ": завершил");

                Thread.Sleep(rand.Next(3000, 7000));
            }
            Dispatcher.Invoke(() => label.Content = "Писатель " + id + 1 + ": остановлен");
        }

        private void QueueAdd(string name)
        {
            Dispatcher.Invoke(() =>
            {
                if (QueueBox.Text == "-") QueueBox.Text = name;
                else QueueBox.Text += " > " + name;
            });
        }

        private void QueueRemove(string name)
        {
            Dispatcher.Invoke(() =>
            {
                var items = QueueBox.Text.Split('>', StringSplitOptions.TrimEntries);
                QueueBox.Text = string.Join(" > ", items.Where(i => i != name));
                if (string.IsNullOrWhiteSpace(QueueBox.Text))
                    QueueBox.Text = "-";
            });
        }
    }
}
