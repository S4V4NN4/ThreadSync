using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using ThreadSync;

namespace ReadersWriters
{
    public partial class MainWindow : Window
    {
        private List<Label> readerLabels = new();
        private List<Label> writerLabels = new();
        private Random rand = new();

        private bool running = false;

        private object lockObj = new();

        int readers = 0;
        int writers = 0;
        int arraySize = 0;
        int?[] value;
        TrackedMutex[] cellLocks;

        int maxReaders = 0;

        //
        // TODO сделать общие данные как список/массив
        // на каждом элементе Mutex. одновременно в ячейку может писать/читать 1 поток
        //
        // писатель записывает только в пустые ячейки
        // читатель считывает ячейку и забирает оттуда значение (т.е. делает пустой)
        //
        // Например: массив из 5 элементов. 5 писателей может одновременно писать, 5 читателей одновременно читать
        // 
        //
        //

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (running)
            {
                return;
            }

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
            if (!int.TryParse(ArraySizeBox.Text, out int arraySize) || arraySize <= 0)
            {
                MessageBox.Show("Введите размер массива (>0)");
                return;
            }

            ReadersPanel.Items.Clear();
            WritersPanel.Items.Clear();
            readerLabels.Clear();
            writerLabels.Clear();
            QueueBox.Text = "-";
            UpdateArrayPanel();

            value = new int?[arraySize];

            cellLocks = new TrackedMutex[arraySize];
            for (int i = 0; i < cellLocks.Length; i++)
            {
                cellLocks[i] = new TrackedMutex();
            }

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
            int? freeCell;
            int? threadValue;

            while (running)
            {
                Dispatcher.Invoke(() => label.Content = "Читатель " + (id + 1) + ": ждёт");
                QueueAdd("R" + id + 1);

                freeCell = ReaderFindFreeCell();
                while (freeCell == null)
                {
                    Thread.Sleep(100);
                    freeCell = ReaderFindFreeCell();
                }

                if (!cellLocks[(int)freeCell].TryAcquire())
                {
                    continue;
                }

                QueueRemove("R" + id + 1);

                Dispatcher.Invoke(() => label.Content = "Читатель " + (id + 1) + ": читает");

                lock (lockObj)
                {
                    Thread.Sleep(rand.Next(2000, 3000));
                    threadValue = value[(int)freeCell];
                    value[(int)freeCell] = null;
                }

                cellLocks[(int)freeCell].Release();

                Dispatcher.Invoke(() => label.Content = "Читатель " + (id + 1) + ": завершил, " + threadValue);
                UpdateArrayPanel();
                Thread.Sleep(rand.Next(3000, 5000));
            }
            Dispatcher.Invoke(() => label.Content = "Читатель " + (id + 1) + ": остановлен");
        }

        private void WriterLoop(int id)
        {
            var label = writerLabels[id];
            int? freeCell;
            while (running)
            {
                Dispatcher.Invoke(() => label.Content = "Писатель " + (id + 1) + ": ждёт");
                QueueAdd("W" + id + 1);

                freeCell = WriterFindFreeCell();
                while (freeCell == null)
                {
                    Thread.Sleep(100);
                    freeCell = WriterFindFreeCell();
                }

                if (!cellLocks[(int)freeCell].TryAcquire())
                {
                    continue;
                }

                QueueRemove("W" + id + 1);

                Dispatcher.Invoke(() => label.Content = "Писатель " + (id + 1) + ": пишет");


                Thread.Sleep(rand.Next(2000, 3000));
                value[(int)freeCell] = rand.Next(0, 1000);
                cellLocks[(int)freeCell].Release();

                Dispatcher.Invoke(() => label.Content = "Писатель " + (id + 1) + ": завершил");
                UpdateArrayPanel();
                Thread.Sleep(rand.Next(5000, 8000));
            }
            Dispatcher.Invoke(() => label.Content = "Писатель " + (id + 1) + ": остановлен");
        }

        private int? ReaderFindFreeCell()
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != null && !cellLocks[i].IsLocked)
                {
                    return i;
                }
            }

            return null;
        }

        private int? WriterFindFreeCell()
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == null && !cellLocks[i].IsLocked)
                {
                    return i;
                }
            }

            return null;
        }

        private void QueueAdd(string name)
        {
            Dispatcher.Invoke(() =>
            {
                if (QueueBox.Text == "-")
                {
                    QueueBox.Text = name;
                }
                else
                {
                    QueueBox.Text += " - " + name;
                }
            });
        }

        private void QueueRemove(string name)
        {
            Dispatcher.Invoke(() =>
            {
                lock (lockObj)
                {
                    var items = QueueBox.Text.Split('-', StringSplitOptions.TrimEntries);
                    QueueBox.Text = string.Join(" - ", items.Where(i => i != name));
                    if (string.IsNullOrWhiteSpace(QueueBox.Text))
                    {
                        QueueBox.Text = "-";
                    }
                }
            });
        }

        private void UpdateArrayPanel()
        {
            Dispatcher.Invoke(() =>
            {
                if (value == null) return;
                ArrayPanel.ItemsSource = value.Select(v => v?.ToString() ?? "·").ToList();
            });
        }
    }
}
