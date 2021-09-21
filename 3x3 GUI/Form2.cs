using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;

namespace _3x3_GUI
{
    public partial class Form2 : Form
    {
        System.DateTime feedback_end_time = DateTime.Now;
        private static EventWaitHandle compWaitHandle;
        bool staleComposite = false;
        static Thread tk;

        List<PictureBox> boxes = new List<PictureBox>();
        List<Button> closeButtons = new List<Button>();
        List<Button> upButtons = new List<Button>();
        List<Button> downButtons = new List<Button>();

        string inputSize = "Full";
        int borderWidth = 6;

        // to do

        // verbose feedback throughout
        // arbitrary x by x
        //   oh but that would require some ui magic

        public Form2()
        {
            InitializeComponent();
            compWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            tk = new Thread(new ThreadStart(updateCompositeLoop));
            tk.Start();

            boxes = flowLayoutPanel2.Controls.OfType<PictureBox>().ToList();
            var buttons = flowLayoutPanel2.Controls.OfType<Button>();
            foreach (Button b in buttons)
            {
                switch (b.Name[0])
                {
                    case 'u':
                        upButtons.Add(b);
                        break;
                    case 'd':
                        downButtons.Add(b);
                        break;
                    case 'c':
                        closeButtons.Add(b);
                        break;
                }
            }
        }

        private void updateCompositeLoop()
        {
            while(true)
            {
                if(staleComposite)
                {
                    try
                    {
                        staleComposite = false;
                        generate_Composite();
                    }
                    catch (Exception)
                    {
                        staleComposite = true;
                        System.Console.WriteLine("wee woo wewweee woo");
                    }
                }
                compWaitHandle.WaitOne();
            }
        }

        private void updateComposite()
        {
            staleComposite = true;
            compWaitHandle.Set();
        }

        private void addButton_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                pictureBox0.Load(openFileDialog1.FileName);
            }
        }

        private void fillImageButton_Click(object sender, EventArgs e)
        {
            // count empty slots
            // if zero, report and cancel
            List<PictureBox> emptyBoxes = getEmptyBoxes();
            if (emptyBoxes.Count == 0)
            {
                showFeedback("All slots full, make space first");
                return;
            }

            // if 1 or more, fill as many as possible
            if(openFileDialog2.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            addImages(openFileDialog2.FileNames);
        }

        private List<PictureBox> getEmptyBoxes()
        {
            List<PictureBox> emptyBoxes = new List<PictureBox>();
            foreach (PictureBox box in boxes)
            {
                if (box.Image == null)
                {
                    emptyBoxes.Add(box);
                }
            }
            return emptyBoxes;
        }

        private void addImages(String[] files)
        {
            List<PictureBox> emptyBoxes = getEmptyBoxes();
            var newImages = Math.Min(emptyBoxes.Count, files.Length);
            // add images to rows
            for (int i = 0; i < newImages; i++)
            {
                var idx = boxes.IndexOf(emptyBoxes[i]);
                addImageRow(idx, files[i]);
            }

            // if overflow, report
            if (newImages < files.Length)
            {
                showFeedback("Slots full. Added " + newImages + " of " + files.Length + " selected images.");
            }
            else
            {
                showFeedback("Added " + newImages + " images.");
            }
            generate_Composite();
            // Not threading here
        }

        private void addImageRow(int row, string fileName)
        {
            boxes[row].Load(fileName);
            setupRowButtons(row);
        }

        private void setupRowButtons(int row)
        {
            if(boxes[row].Image != null)
            {
                if (row > 0)
                {
                    upButtons[row].Enabled = true;
                }
                if (row < 8)
                {
                    downButtons[row].Enabled = true;
                }
                closeButtons[row].Enabled = true;
            }
            else
            {
                upButtons[row].Enabled = false;
                downButtons[row].Enabled = false;
                closeButtons[row].Enabled = false;
            }
        }

        private void removeImageRow(int row)
        {
            boxes[row].Image = null;
            upButtons[row].Enabled = false;
            downButtons[row].Enabled = false;
            closeButtons[row].Enabled = false;

            updateComposite();
        }

        private void swapRowUp(int row)
        {
            if(row == 0)
            {
                return;
            }

            flipRows(row, row - 1);
            setupRowButtons(row);
            setupRowButtons(row - 1);

            updateComposite();
        }

        private void swapRowDown(int row)
        {
            if(row == 8)
            {
                return;
            }
            flipRows(row, row + 1);
            setupRowButtons(row);
            setupRowButtons(row + 1);

            updateComposite();
        }

        private void flipRows(int from, int to)
        {
            var temp = boxes[to].Image;
            boxes[to].Image = boxes[from].Image;
            boxes[from].Image = temp;
        }

        private void showFeedback(string text)
        {
            feedbackBox.Text = text;
            feedbackTimer.Enabled = true;
            feedback_end_time = DateTime.Now.AddSeconds(3);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (DateTime.Now.CompareTo(feedback_end_time) > 0)
            {
                feedbackTimer.Enabled = false;
                feedbackBox.Text = "";
            }
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            updateComposite();
        }

        private void generate_Composite()
        {
            // Gather images
            List<Image> pics = new List<Image>();
            var sectionHeight = 1080;
            foreach (PictureBox box in boxes)
            {
                if(box.Image == null)
                {
                    pics.Add(null);
                }
                else
                {
                    //System.InvalidOperationException
                    pics.Add(new Bitmap(box.Image));

                    // Important. If the image is wider than 16:9, can't just pull the height.
                    // Need to figure out the local section height, sections being 16:9 chunks on the board
                    var localHeight = box.Image.Height;
                    if((float)box.Image.Width / box.Image.Height > 16.0/9.0)
                    {
                        localHeight = (int)(9.0 / 16.0 * box.Image.Width);
                    }
                    if (localHeight < sectionHeight)
                    {
                        sectionHeight = box.Image.Height;
                    }
                }
            }

            // Resize all images to match smallest one
            if(sectionHeight < 1080)
            {
                for(int i = 0; i < pics.Count; i++)
                {
                    if(pics[i] == null)
                    {
                        continue;
                    }
                    else
                    {
                        // If wider, fix width, calculate height
                        if ((float)pics[i].Width / pics[i].Height > 16.0 / 9.0)
                        {
                            var localWidth = (16.0 / 9 * sectionHeight);
                            var localHeight = (float)pics[i].Height / pics[i].Width * localWidth;
                            var old = pics[i];
                            pics[i] = ResizeImage(old, (int)localWidth, (int)localHeight);
                            old.Dispose();
                        }
                        else    // fix height, calculate width
                        {
                            var pic_width = (float)pics[i].Width / pics[i].Height * sectionHeight;
                            var old = pics[i];
                            pics[i] = ResizeImage(old, (int)pic_width, sectionHeight);
                            old.Dispose();
                        }
                    }
                }
            }

            // Combine images
            var sectionWidth = (int)((float)1920 / 1080 * sectionHeight);
            var composite = new Bitmap(sectionWidth * 3, sectionHeight * 3);
            using (var g = Graphics.FromImage(composite))
            {
                for (int i = 0; i < pics.Count; i++)
                {
                    // Top left corner of section
                    var x = sectionWidth * (i % 3);
                    var y = sectionHeight * (i / 3);
                    if (pics[i] == null)
                    {
                        g.FillRectangle(Brushes.White, x, y, sectionWidth, sectionHeight);       // White for empty slots
                    }
                    else
                    {
                        g.FillRectangle(Brushes.Black, x, y, sectionWidth, sectionHeight);       // Black for backgrounds
                        // Top left corner of image
                        var dx = x + (sectionWidth - pics[i].Width) / 2;
                        var dy = y + (sectionHeight - pics[i].Height) / 2;
                        g.DrawImage(pics[i], new Point(dx, dy));
                    }
                }
            }

            // Adjust for output size
            switch(inputSize)
            {
                case "1920x1080":
                    var old = composite;
                    composite = ResizeImage(old, 1920, 1080);
                    old.Dispose();
                    break;
                // future cases here
            }

            // Add lines
            if(borderWidth > 0)
            {
                using (var g = Graphics.FromImage(composite))
                {
                    var pen = new Pen(Color.White)
                    {
                        Width = borderWidth
                    };
                    var dx = composite.Width / 3;
                    var dy = composite.Height / 3;
                    // Vertical
                    g.DrawLine(pen, new Point(dx, 0),     new Point(dx, dy * 3));
                    g.DrawLine(pen, new Point(dx * 2, 0), new Point(dx * 2, dy * 3));
                    // Horizontal
                    g.DrawLine(pen, new Point(0, dy),     new Point(dx * 3, dy));
                    g.DrawLine(pen, new Point(0, dy * 2), new Point(dx * 3, dy * 2));
                }
            }

            // Finally, save and clean up
            {
                var old = pictureBox0.Image;
                pictureBox0.Image = composite;
                if (old != null)
                {
                    old.Dispose();
                }
                for (int i = 0; i < pics.Count; i++)
                {
                    if (pics[i] != null)
                    {
                        var t = pics[i];
                        pics[i] = null;
                        t.Dispose();
                    }
                }
            }
        }

        private Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            saveFileDialog1.ShowDialog();
            if(saveFileDialog1.FileName != "")
            {
                System.IO.FileStream fs = (System.IO.FileStream)saveFileDialog1.OpenFile();
                pictureBox0.Image.Save(fs, ImageFormat.Png);
                fs.Close();
            }
            showFeedback("File saved");
        }

        private void borderWidthBoxEntry_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == ((char)Keys.Enter))
            {
                int new_w;
                try
                {
                    new_w = int.Parse(borderWidthBoxEntry.Text);
                }
                catch (Exception)
                {
                    borderWidthBoxEntry.Text = borderWidth.ToString();
                    showFeedback("Invalid entry, requires int");
                    return;
                }
                if(new_w < 0)
                {
                    borderWidthBoxEntry.Text = borderWidth.ToString();
                    showFeedback("Invalid entry, requires positive number");
                    return;
                }

                // Success
                showFeedback("Border width set to " + new_w.ToString() + " pixels");
                borderWidth = new_w;
                updateComposite();
            }
        }
        private void outputSizeInput_SelectedIndexChanged(object sender, EventArgs e)
        {
            inputSize = outputSizeInput.Text;
            updateComposite();
            showFeedback("Current output dimensions: " + pictureBox0.Image.Width + "x" + pictureBox0.Image.Height);
        }

        private void Form2_DragDrop(object sender, DragEventArgs e)
        {
            String[] filenames = (String[])e.Data.GetData(DataFormats.FileDrop);
            addImages(filenames);
        }

        private void Form2_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                List<PictureBox> emptyBoxes = getEmptyBoxes();
                if (emptyBoxes.Count == 0)
                {
                    showFeedback("All slots full, make space first");
                    e.Effect = DragDropEffects.None;
                    return;
                }

                e.Effect = DragDropEffects.Copy;
            }
            else
                e.Effect = DragDropEffects.None;
        }

        private void clearButton1_Click(object sender, EventArgs e)
        {
            removeImageRow(0);
        }

        private void clearButton2_Click(object sender, EventArgs e)
        {
            removeImageRow(1);
        }

        private void clearButton3_Click(object sender, EventArgs e)
        {
            removeImageRow(2);
        }

        private void clearButton4_Click(object sender, EventArgs e)
        {
            removeImageRow(3);
        }

        private void clearButton5_Click(object sender, EventArgs e)
        {
            removeImageRow(4);
        }

        private void clearButton6_Click(object sender, EventArgs e)
        {
            removeImageRow(5);
        }

        private void clearButton7_Click(object sender, EventArgs e)
        {
            removeImageRow(6);
        }

        private void clearButton8_Click(object sender, EventArgs e)
        {
            removeImageRow(7);
        }

        private void clearButton9_Click(object sender, EventArgs e)
        {
            removeImageRow(8);
        }

        private void upButton1_Click(object sender, EventArgs e)
        {
            swapRowUp(0);
        }

        private void upButton2_Click(object sender, EventArgs e)
        {
            swapRowUp(1);
        }

        private void upButton3_Click(object sender, EventArgs e)
        {
            swapRowUp(2);
        }

        private void upButton4_Click(object sender, EventArgs e)
        {
            swapRowUp(3);
        }

        private void upButton5_Click(object sender, EventArgs e)
        {
            swapRowUp(4);
        }

        private void upButton6_Click(object sender, EventArgs e)
        {
            swapRowUp(5);
        }

        private void upButton7_Click(object sender, EventArgs e)
        {
            swapRowUp(6);
        }

        private void upButton8_Click(object sender, EventArgs e)
        {
            swapRowUp(7);
        }

        private void upButton9_Click(object sender, EventArgs e)
        {
            swapRowUp(8);
        }

        private void downButton1_Click(object sender, EventArgs e)
        {
            swapRowDown(0);
        }

        private void downButton2_Click(object sender, EventArgs e)
        {
            swapRowDown(1);
        }

        private void downButton3_Click(object sender, EventArgs e)
        {
            swapRowDown(2);
        }

        private void downButton4_Click(object sender, EventArgs e)
        {
            swapRowDown(3);
        }

        private void downButton5_Click(object sender, EventArgs e)
        {
            swapRowDown(4);
        }

        private void downButton6_Click(object sender, EventArgs e)
        {
            swapRowDown(5);
        }

        private void downButton7_Click(object sender, EventArgs e)
        {
            swapRowDown(6);
        }

        private void downButton8_Click(object sender, EventArgs e)
        {
            swapRowDown(7);
        }

        private void downButton9_Click(object sender, EventArgs e)
        {
            swapRowDown(8);
        }

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            tk.Abort();
        }

    }
}
