using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
//using System.Windows.Forms.DataVisualization;
//using System.Windows.Forms.DataVisualization.Charting;

namespace MarketChart
{
    public partial class Form1 : Form
    {
        private static readonly HttpClient client = new HttpClient();

        public List<float> points = new List<float>();
        private Timer timer1;
        private Timer timerCountSeconds;
        private DateTime startTime;
        public int interval = 300000;
        int ticks = 0;
        int numUpdates = 0;

        protected struct coinData
        {
            public string name;
            public double marketCap;
            public double price;
            public double volume;
            public double fiveMin;
            public double hr;
            public double day;
            public double week;
            public int posTicks;
            public int negTicks;
            public List<int> posTicks10;
            public List<int> negTicks10;
            public double upRatio;
            public double upRatio10;
        }

        protected struct snapShot
        {
            public DateTime timeStamp;
            public List<coinData> coins;
        }

        List<snapShot> snapShots = new List<snapShot>();


        public Form1()
        {
            InitializeComponent();

            DataGridViewCheckBoxColumn checkColumn = new DataGridViewCheckBoxColumn();
            checkColumn.Name = "Visible";
            checkColumn.HeaderText = "Visible";
            checkColumn.Width = 50;
            checkColumn.ReadOnly = false;
            checkColumn.FillWeight = 10;
            checkColumn.TrueValue = true;
            checkColumn.FalseValue = false;
            dataGridView1.Columns.Add(checkColumn);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (timer1 == null)
            {
                InitTimer();
                button1.Text = "Stop";

                timer1_Tick(new object(), new EventArgs());
            }
            else
            {
                timer1.Stop();
                timer1 = null;
                button1.Text = "Start";
            }
        }

        public void InitTimer()
        {
            timer1 = new Timer();
            timer1.Tick += new EventHandler(timer1_Tick);
            timer1.Interval = GetIntervalSeconds() * 1000; // in miliseconds
            timer1.Start();

            timerCountSeconds = new Timer();
            timerCountSeconds.Tick += new EventHandler(timerSeconds_Tick);
            timerCountSeconds.Interval = 1000; // in miliseconds
            timerCountSeconds.Start();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            ticks++;

            startTime = DateTime.Now;
            timer1.Interval = GetIntervalSeconds() * 1000;

            getHttpCMC();
        }

        private void timerSeconds_Tick(object sender, EventArgs e)
        {
            int elapsedSeconds = (int)(DateTime.Now - startTime).TotalSeconds;

            if(!label1.Text.Contains("Updating"))
            {
                int secondsLeft = (timer1.Interval/1000) - elapsedSeconds;

                label1.Text = secondsLeft + " seconds until next update.";
            }
        }

        private int GetIntervalSeconds()
        {
            float duration = 3;

            float.TryParse(txtRefresh.Text, out duration);

            return (int)duration * 60;
        }

        private void updateGraph()
        {
            ChartAreas();
            ChartSeries();

            chart1.Invalidate();
        }


        private void ChartAreas()
        {
            var axisX = new System.Windows.Forms.DataVisualization.Charting.Axis
            {
                Interval = 1,
            };

            double min = double.MaxValue;
            double max = 0;

            if (points.Count > 0)
            {
                //min = (int)points.Min();
                //max = (int)points.Max();
            }

            min = -0.1;
            max = 0.1;

            foreach (snapShot s in snapShots)
                foreach (coinData c in s.coins)
                {
                    if (c.fiveMin > max)
                        max = Math.Round((double)c.fiveMin, 2);

                    if (c.fiveMin < min && c.fiveMin > 0)
                        min = Math.Round((double)c.fiveMin, 2);
                }

            //min = -1;
            //max = 1;

            var axisY = new System.Windows.Forms.DataVisualization.Charting.Axis
            {
                Minimum = min,
                Maximum = max,
                Title = "% gain BTC",
            };

            var chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea
            {
                AxisX = axisX,
                AxisY = axisY,
            };

            chartArea1.AxisX.LabelStyle.Format = "dd/MMM\nhh:mm";
            //chartArea1.AxisX.LabelStyle.Format = "hh:mm";


            chartArea1.AxisX.MajorGrid.Enabled = false;
            chartArea1.AxisY.MajorGrid.Enabled = false;
            chartArea1.AxisX.LabelStyle.Enabled = false;

            this.chart1.ChartAreas.Clear();
            this.chart1.ChartAreas.Add(chartArea1);
        }


        private void ChartSeries()
        {
            chart1.Series.Clear();

            List<string> coinNames = new List<string>();

            if (snapShots.Count > 0)
                foreach (coinData c in snapShots[0].coins)
                    coinNames.Add(c.name);

            for (int k = 0; k < coinNames.Count; k++)
            {
                var series1 = new System.Windows.Forms.DataVisualization.Charting.Series
                {
                    Name = coinNames[k],
                    Color = Color.FromArgb(255, (k * 17) % 255, (k * 31) % 255, (k * 61) % 255),
                    BorderWidth = 1,
                    IsXValueIndexed = false,
                    ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line,
                };

                for (int i = 0; i < snapShots.Count; i++)
                {
                    for (int j = 0; j < snapShots[i].coins.Count; j++)
                    {
                        if (snapShots[i].coins[j].name == coinNames[k])
                        {
                            series1.Points.AddXY(i, snapShots[i].coins[j].fiveMin);

                        }
                    }
                }

                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    DataGridViewCheckBoxCell chk = (DataGridViewCheckBoxCell)row.Cells[2];

                    if (row.Cells[0].Value.ToString() == series1.Name &&
                        chk.Value == chk.TrueValue)
                        chart1.Series.Add(series1);
                }
            }
        }



        public async void getHttpCMC()
        {
            label1.Text = "Updating";

            var responseString = "";

            try
            {
                responseString = await client.GetStringAsync("https://coinmarketcap.com/all/views/all/");
            }
            catch (TaskCanceledException e1)
            {
                //txtStatus.Text = e1.Message + "\n" + txtStatus.Text;
                return;
            }
            catch (HttpRequestException e1)
            {
                //txtStatus.Text = e1.Message + "\n" + txtStatus.Text;
                return;
            }

            if (snapShots.Count > 0)
                numUpdates++;

            List<string> checkedCoins = new List<string>();
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                DataGridViewCheckBoxCell chk = (DataGridViewCheckBoxCell)row.Cells[2];

                if (chk.Value == chk.TrueValue)
                    checkedCoins.Add(row.Cells[0].Value.ToString());
            }

            dataGridView1.Rows.Clear();

            //Debug.Print(responseString);

            string phrase = "<tbody>";
            int startIdx = responseString.IndexOf(phrase) + phrase.Length;
            phrase = "</tbody>";
            int endIdx = responseString.IndexOf(phrase);
            responseString = responseString.Substring(startIdx, endIdx - startIdx);

            //Debug.Print(responseString);

            snapShot s;

            s.timeStamp = DateTime.Now;
            s.coins = new List<coinData>();


            while (responseString.Contains("</tr>"))
            {
                coinData c;
                c.posTicks10 = new List<int>();
                c.negTicks10 = new List<int>();

                phrase = "<tr id=\"id-";
                startIdx = responseString.IndexOf(phrase) + phrase.Length;
                phrase = "\"  class=";
                endIdx = responseString.IndexOf(phrase);

                if (endIdx < 0)
                    endIdx = 10;
                string coinName = responseString.Substring(startIdx, endIdx - startIdx);
                responseString = responseString.Substring(endIdx + phrase.Length, responseString.Length - (endIdx + phrase.Length));

                phrase = "data-btc=\"";
                startIdx = responseString.IndexOf(phrase) + phrase.Length;
                phrase = "\" >";
                endIdx = responseString.IndexOf(phrase);

                if (endIdx < 0)
                    endIdx = 10;
                string strMarketCap = responseString.Substring(startIdx, endIdx - startIdx);
                double marketCap = 0;
                double.TryParse(strMarketCap, out marketCap);
                responseString = responseString.Substring(endIdx + phrase.Length, responseString.Length - (endIdx + phrase.Length));

                phrase = "data-btc=\"";
                startIdx = responseString.IndexOf(phrase) + phrase.Length;
                phrase = "\" >";
                endIdx = responseString.IndexOf(phrase);

                if (endIdx < 0)
                    endIdx = 10;
                string strPrice = responseString.Substring(startIdx, endIdx - startIdx);
                double price = 0;
                double.TryParse(strPrice, out price);
                responseString = responseString.Substring(endIdx + phrase.Length, responseString.Length - (endIdx + phrase.Length));

                phrase = "data-btc=\"";
                startIdx = responseString.IndexOf(phrase) + phrase.Length;
                phrase = "\" >";
                endIdx = responseString.IndexOf(phrase);

                if (endIdx < 0)
                    endIdx = 10;
                string strVolume = responseString.Substring(startIdx, endIdx - startIdx);
                double volume = 0;
                double.TryParse(strVolume, out volume);
                responseString = responseString.Substring(endIdx + phrase.Length, responseString.Length - (endIdx + phrase.Length));

                phrase = "data-btc=\"";
                startIdx = responseString.IndexOf(phrase) + phrase.Length;
                phrase = "\" >";
                endIdx = responseString.IndexOf(phrase);

                if (endIdx < 0)
                    endIdx = 10;
                string strHr = responseString.Substring(startIdx, endIdx - startIdx);
                double hr = 0;
                double.TryParse(strHr, out hr);
                responseString = responseString.Substring(endIdx + phrase.Length, responseString.Length - (endIdx + phrase.Length));

                phrase = "data-btc=\"";
                startIdx = responseString.IndexOf(phrase) + phrase.Length;
                phrase = "\" >";
                endIdx = responseString.IndexOf(phrase);

                if (endIdx < 0)
                    endIdx = 10;
                string strDay = responseString.Substring(startIdx, endIdx - startIdx);
                double day = 0;
                double.TryParse(strDay, out day);
                responseString = responseString.Substring(endIdx + phrase.Length, responseString.Length - (endIdx + phrase.Length));

                phrase = "data-btc=\"";
                startIdx = responseString.IndexOf(phrase) + phrase.Length;
                phrase = "\" >";
                endIdx = responseString.IndexOf(phrase);

                if (endIdx < 0)
                    endIdx = 10;
                string strWeek = responseString.Substring(startIdx, endIdx - startIdx);
                double week = 0;
                double.TryParse(strWeek, out week);
                responseString = responseString.Substring(endIdx + phrase.Length, responseString.Length - (endIdx + phrase.Length));

                phrase = "</tr>";
                startIdx = responseString.IndexOf(phrase) + phrase.Length;
                responseString = responseString.Substring(startIdx + phrase.Length, responseString.Length - (startIdx + phrase.Length));

                c.name = coinName;
                c.marketCap = marketCap;
                c.price = price;
                c.volume = volume;
                c.hr = hr;
                c.day = day;
                c.week = week;

                c.fiveMin = 0;
                c.posTicks = 0;
                c.negTicks = 0;
                c.upRatio = 0;
                c.upRatio10 = 0;

                if (snapShots.Count > 0)
                {
                    foreach (coinData d in snapShots[snapShots.Count - 1].coins)
                        if (d.name == c.name)
                        {
                            c.fiveMin = d.fiveMin + ((price - d.price) / price);

                            c.posTicks = d.posTicks;
                            c.negTicks = d.negTicks;
                            c.posTicks10 = d.posTicks10;
                            c.negTicks10 = d.negTicks10;

                            if (c.fiveMin > 0)
                            {
                                c.posTicks++;
                                c.posTicks10.Add(1);
                                c.negTicks10.Add(0);
                            }
                            else if (c.fiveMin < 0)
                            {
                                c.negTicks++;
                                c.posTicks10.Add(0);
                                c.negTicks10.Add(1);
                            }
                            else
                            {
                                c.posTicks10.Add(0);
                                c.negTicks10.Add(0);
                            }

                            if (c.posTicks10.Count > 10)
                                c.posTicks10.RemoveAt(0);

                            if (c.negTicks10.Count > 10)
                                c.negTicks10.RemoveAt(0);

                            c.upRatio = (c.posTicks - c.negTicks) / (double)numUpdates;

                            int pos10sum = 0;
                            int neg10sum = 0;

                            foreach (int i in c.posTicks10)
                                pos10sum += i;

                            foreach (int i in c.negTicks10)
                                neg10sum += i;

                            c.upRatio10 = (pos10sum - neg10sum) / Math.Min((double)c.posTicks10.Count, 10.0);
                        }
                }


                if (c.volume > 300)
                {
                    s.coins.Add(c);


                    dataGridView1.Rows.Add(c.name, c.fiveMin);
                }
            }

            dataGridView1.Sort(dataGridView1.Columns[1], ListSortDirection.Descending);

            snapShots.Add(s);

            if (snapShots.Count > 400)
                snapShots.RemoveAt(0);






            if (ticks == 1)
                foreach (DataGridViewRow row in dataGridView1.Rows)
                {
                    DataGridViewCheckBoxCell chk = (DataGridViewCheckBoxCell)row.Cells[2];
                    chk.Value = chk.TrueValue;
                }
            else
                foreach (string str in checkedCoins)
                {
                    foreach (DataGridViewRow row in dataGridView1.Rows)
                    {
                        if (row.Cells[0].Value.ToString() == str)
                        {
                            DataGridViewCheckBoxCell chk = (DataGridViewCheckBoxCell)row.Cells[2];
                            chk.Value = chk.TrueValue;
                        }
                    }
                }

            updateGraph();

            label1.Text = "";


            /*
            strLog = "name" + ",," + "marketCap" + ",," + "price" + ",," + "volume" + ",," + "5min" + ",," + "hr" + ",," + "day" + ",," + "week" + ",," + "upRatio" + ",," + "upRatio10" + "\n";

            foreach (coinData c in s.coins)
                strLog += c.name + ",," + c.marketCap + ",," + c.price.ToString("N10") + ",," + c.volume + ",," + c.fiveMin + ",," + c.hr + ",," + c.day + ",," + c.week + ",," + c.upRatio + ",," + c.upRatio10 + "\n";

            logPath = DateTime.Now.ToString().Replace("/", "-").Replace(":", ".") + ".csv";

            File.AppendAllText(logPath, strLog);


            Stats();
            */

        }

        private void btnCheckAll_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                DataGridViewCheckBoxCell chk = (DataGridViewCheckBoxCell)row.Cells[2];
                chk.Value = chk.TrueValue;
            }

            updateGraph();
        }

        private void btnUncheckAll_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                DataGridViewCheckBoxCell chk = (DataGridViewCheckBoxCell)row.Cells[2];
                chk.Value = chk.FalseValue;
            }

            updateGraph();
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            updateGraph();
        }

        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridView1.IsCurrentCellDirty)
            {
                dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }
    }
}