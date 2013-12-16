﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.Reflection;
using System.IO;
using System.Drawing.Imaging;

using System.Security.Cryptography;

//AmiMat
using Amimat.Core;
//Json
using Newtonsoft.Json;

namespace Animation_Creator
{
    enum State
    {
        EMPTY,
        LOADED,
        READY
    }
    public partial class MainWindow : Form
    {
        public MainWindow(string [] args)
        {
            InitializeComponent();
            Arguments = args;
        }

        string WorkingDir = @"";
        string [] Arguments = null;
        MD5CryptoServiceProvider MD5 = new MD5CryptoServiceProvider();
        List<byte[]> Frames = new List<byte[]>() { };
        AMTAnimation Animation = null;

        State ProgramState = State.EMPTY;

        private void MainWindow_Load(object sender, EventArgs e)
        {
            ClearElements();
            InitData();
            ProgramState = State.EMPTY;
            if (Arguments != null && Arguments.Length > 0)
            {
                string filename = Arguments[0];
                if (File.Exists(filename))
                {
                    Animation = new AMTAnimation();
                    OpenProject(filename);
                    PopulateUI();
                }
            }
        }
        private void ClearElements()
        {
            lblFrameCount.Text = "0";
            lbGifFrames.Items.Clear();
            ClearPictureBoxImage();
            lblMd5.Text = "MD5";
            lbActions.Items.Clear();
            lblCurrentAction.Text = "null";
            lbFrames.Items.Clear();
            tssAsset.Text = "Ready";
            tssWorkingDir.Text = "...";
        }
        private void InitData()
        {
            Animation = null;
            tAutoSave.Interval = 1000 * 60 * 2; //2min autosave cycle
        }
        private void LoadGif(string pathToImage)
        {
            try
            {
                //Try extracting the frames
                Frames = EnumerateFrames(pathToImage);
                if (Frames == null || Frames.Count() == 0)
                {
                    throw new NoNullAllowedException("Unable to obtain frames from " + pathToImage);
                }

                lblFrameCount.Text = Frames.Count().ToString();

                for (int i = 0; i < Frames.Count(); i++)
                {
                    lbGifFrames.Items.Add(i.ToString());
                }

                lbGifFrames.SelectedIndex = 0;

            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error type: " + ex.GetType().ToString() + "\n" +
                    "Message: " + ex.Message,
                    "Error in " + MethodBase.GetCurrentMethod().Name
                    );
            }
        }
        private List<byte[]> EnumerateFrames(string imagePath)
        {
            try
            {
                //Make sure the image exists
                if (!File.Exists(imagePath))
                {
                    throw new FileNotFoundException("Unable to locate " + imagePath);
                }

                Dictionary<Guid, ImageFormat> guidToImageFormatMap = new Dictionary<Guid, ImageFormat>()
                {
                    {ImageFormat.Bmp.Guid,  ImageFormat.Bmp},
                    {ImageFormat.Gif.Guid,  ImageFormat.Png},
                    {ImageFormat.Icon.Guid, ImageFormat.Png},
                    {ImageFormat.Jpeg.Guid, ImageFormat.Jpeg},
                    {ImageFormat.Png.Guid,  ImageFormat.Png}
                };

                List<byte[]> tmpFrames = new List<byte[]>() { };

                using (Image img = Image.FromFile(imagePath, true))
                {
                    //Check the image format to determine what
                    //format the image will be saved to the 
                    //memory stream in
                    ImageFormat imageFormat = null;
                    Guid imageGuid = img.RawFormat.Guid;

                    foreach (KeyValuePair<Guid, ImageFormat> pair in guidToImageFormatMap)
                    {
                        if (imageGuid == pair.Key)
                        {
                            imageFormat = pair.Value;
                            break;
                        }
                    }

                    if (imageFormat == null)
                    {
                        throw new NoNullAllowedException("Unable to determine image format");
                    }

                    //Get the frame count
                    FrameDimension dimension = new FrameDimension(img.FrameDimensionsList[0]);
                    int frameCount = img.GetFrameCount(dimension);

                    //Step through each frame
                    for (int i = 0; i < frameCount; i++)
                    {
                        //Set the active frame of the image and then 
                        //write the bytes to the tmpFrames array
                        img.SelectActiveFrame(dimension, i);
                        using (MemoryStream ms = new MemoryStream())
                        {

                            img.Save(ms, imageFormat);
                            tmpFrames.Add(ms.ToArray());
                        }
                    }

                }

                return tmpFrames;

            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error type: " + ex.GetType().ToString() + "\n" +
                    "Message: " + ex.Message,
                    "Error in " + MethodBase.GetCurrentMethod().Name
                    );
            }

            return null;
        }
        private Bitmap ConvertBytesToImage(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return null;
            }

            try
            {
                //Read bytes into a MemoryStream
                using (MemoryStream ms = new MemoryStream(imageBytes))
                {
                    //Recreate the frame from the MemoryStream
                    using (Bitmap bmp = new Bitmap(ms))
                    {
                        return (Bitmap)bmp.Clone();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error type: " + ex.GetType().ToString() + "\n" +
                    "Message: " + ex.Message,
                    "Error in " + MethodBase.GetCurrentMethod().Name
                    );
            }

            return null;
        }
        private byte[] ConvertImageToBytes(Image image)
        {
            if (image == null)
            {
                return null;
            }

            try
            {
                //Read bytes into a MemoryStream
                using (MemoryStream ms = new MemoryStream())
                {
                    image.Save(ms, System.Drawing.Imaging.ImageFormat.Gif);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error type: " + ex.GetType().ToString() + "\n" +
                    "Message: " + ex.Message,
                    "Error in " + MethodBase.GetCurrentMethod().Name
                    );
            }
            return null;
        }
        private void ClearPictureBoxImage()
        {
            try
            {
                if (pbFrame.Image != null)
                {
                    pbFrame.Image.Dispose();
                }
                pbFrame.Image = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error type: " + ex.GetType().ToString() + "\n" +
                    "Message: " + ex.Message,
                    "Error in " + MethodBase.GetCurrentMethod().Name
                    );
            }
        }
        private string ImageMD5(Image image)
        {
            byte[] md5Hash = MD5.ComputeHash(ConvertImageToBytes(image));
            StringBuilder sb = new StringBuilder();
            foreach (byte b in md5Hash)
            {
                sb.Append(b.ToString("x2").ToLower());
            }
            return sb.ToString();
        }
        static void Swap<T>(IList<T> list, int indexA, int indexB)
        {
            T tmp = list[indexA];
            list[indexA] = list[indexB];
            list[indexB] = tmp;
        }
        private void lbFrames_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (lbGifFrames.SelectedIndex == -1)
                {
                    return;
                }

                //Make sure frames have been extracted
                if (Frames == null || Frames.Count() == 0)
                {
                    throw new NoNullAllowedException("Frames have not been extracted");
                }

                //Make sure the selected index is within range
                if (lbGifFrames.SelectedIndex > Frames.Count() - 1)
                {
                    throw new IndexOutOfRangeException("Frame list does not contain index: " + lbGifFrames.SelectedIndex.ToString());
                }

                //Clear the PictureBox
                ClearPictureBoxImage();

                //Load the image from the byte array
                pbFrame.Image = ConvertBytesToImage(Frames[lbGifFrames.SelectedIndex]);

                lblMd5.Text = "MD5: " + ImageMD5(pbFrame.Image);

            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error type: " + ex.GetType().ToString() + "\n" +
                    "Message: " + ex.Message,
                    "Error in " + MethodBase.GetCurrentMethod().Name
                    );
            }
        }
        private void InitManifest()
        {
            Animation = new AMTAnimation();
            Animation.Manifest.AssetName = "asset.gif";
            Animation.Manifest.ActionFileName.Add("null.act");
            Animation.Actions.Add(new AMTAction());
            Animation.Actions[0].Name = "null";
            Animation.Actions[0].Frames.Add(new AMTFrame());
            Animation.Actions[0].Frames[0].Delay = (int)nudDefaultDelay.Value;
            Animation.Actions[0].Frames[0].FrameRef = 0;
            Animation.Actions[0].Frames[0].Tags.Add("null");
            Animation.Actions[0].Frames[0].MD5 = ImageMD5(ConvertBytesToImage(Frames[0]));
            //First Time Save
            Save();
            PopulateUI();
            ProgramState = State.READY;
        }
        private string FrameToString(AMTFrame frame)
        {
            string str = "";
            if (frame.ActionRef == null)
            {
                str = "Frame Reference: [" + frame.FrameRef + "]" + "Frame Delay: " + "[" + frame.Delay + "ms" + "]";
                str += "Tags: [";
                foreach (string s in frame.Tags)
                {
                    str += "(";
                    str += s;
                    str += ")";
                }
                str += "]";
            }
            else
            {
                str = "Action Reference: [" + frame.ActionRef + "]";
            }
            return str;
        }
        private static string GetNumbers(string input)
        {
            return new string(input.Where(c => char.IsDigit(c)).ToArray());
        }
        private void PopulateAction()
        {
            lbActions.Items.Clear();
            //lbFrames.Items.Clear();
            foreach (string actionName in Animation.Manifest.ActionFileName)
            {
                lbActions.Items.Add(actionName);
            }
            /*lbActions.SelectedIndex = 0;
            foreach (AMTFrame frame in Animation.Actions[lbActions.SelectedIndex].Frames)
            {
                lbFrames.Items.Add(FrameToString(frame));
            }*/
        }
        private void PopulateFrames()
        {
            lbFrames.Items.Clear();
            foreach (AMTFrame frame in Animation.Actions[lbActions.SelectedIndex].Frames)
            {
                lbFrames.Items.Add(FrameToString(frame));
            }
        }
        private void PopulateUI()
        {
            PopulateAction();
            lbActions.SelectedIndex = 0;
            PopulateFrames();
        }
        private void Save()
        {
            //Serialize
            File.WriteAllText(Path.Combine(WorkingDir, "AMT.amf"), JsonConvert.SerializeObject(Animation.Manifest, Formatting.Indented));
            foreach(AMTAction a in Animation.Actions)
            {
                File.WriteAllText(Path.Combine(WorkingDir, a.Name + ".act"), JsonConvert.SerializeObject(a, Formatting.Indented));
            }
            lblAutoSave.Text = DateTime.Now.ToString();
        }
        private void OpenProject(string FileName)
        {
            WorkingDir = Path.GetDirectoryName(FileName);
            if (!File.Exists(Path.Combine(WorkingDir, "null.act")))
            {
                MessageBox.Show("Your working directory does not include null action!", "Error!",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else
            {
                string AMTMF = File.ReadAllText(FileName);
                try
                {
                    Animation.Manifest = JsonConvert.DeserializeObject<AMTManifest>(AMTMF);
                }
                catch
                {
                    MessageBox.Show("Project cannot be opened!", "Project Type Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!File.Exists(Path.Combine(WorkingDir, Animation.Manifest.AssetName)))
                {
                    MessageBox.Show("Asset does not exist!", "Error!",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                else
                {
                    //Loop Load
                    foreach (string s in Animation.Manifest.ActionFileName)
                    {
                        Animation.Actions.Add(JsonConvert.DeserializeObject<AMTAction>
                                             (File.ReadAllText(Path.Combine(WorkingDir, s))));
                    }
                    LoadGif(Path.Combine(WorkingDir, Animation.Manifest.AssetName));
                    tssAsset.Text = FileName;
                    tssWorkingDir.Text = WorkingDir;
                }
            }
            PopulateUI();
            ProgramState = State.READY;
            //Check existance of AMT.amf existance
            //First check loaded asset with asset set in AMT.amf
            //Load and deserialize object
        }

        private AMTAction ExpandFrame(AMTAction Action)
        {
            AMTAction ExpandedAction = new AMTAction();
            ExpandedAction.Name = Action.Name;
            ExpandedAction.Frames.Clear();
            foreach (AMTFrame f in Action.Frames)
            {
                if (f.ActionRef != null)
                {
                    AMTAction EmbeddedAction = Animation.Actions[Animation.Manifest.ActionFileName.IndexOf(f.ActionRef)];
                    AMTAction ExpandedEmbeddedAction = ExpandFrame(EmbeddedAction);
                    foreach (AMTFrame fe in ExpandedEmbeddedAction.Frames)
                    {
                        ExpandedAction.Frames.Add(fe);
                    }
                }
                else
                {
                    ExpandedAction.Frames.Add(f);
                }

            }
            return ExpandedAction;
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog OpenFileDialog = new OpenFileDialog();
            OpenFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            OpenFileDialog.Filter = "gif files (*.gif)|*.*";
            OpenFileDialog.FilterIndex = 2;
            OpenFileDialog.RestoreDirectory = true;
            if (OpenFileDialog.ShowDialog() == DialogResult.OK)
            {
                ProgramState = State.LOADED;
                //Clear UI Before this and Data
                ClearElements();
                InitData();
                LoadGif(OpenFileDialog.FileName);
                tssAsset.Text = OpenFileDialog.FileName;
                //Creating New Dir
                WorkingDir = Path.GetDirectoryName(OpenFileDialog.FileName);
                WorkingDir = Directory.CreateDirectory(Path.Combine(WorkingDir, Path.GetFileNameWithoutExtension(OpenFileDialog.FileName))).FullName;
                tssWorkingDir.Text = WorkingDir;
                //Copy
                File.Copy(OpenFileDialog.FileName, Path.Combine(WorkingDir, "asset.gif"));
                InitManifest();
            }
        }

        private void btnOpenExisting_Click(object sender, EventArgs e)
        {
            OpenFileDialog OpenFileDialog = new OpenFileDialog();
            OpenFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            OpenFileDialog.Filter = "amf files (*.amf)|*.amf";
            OpenFileDialog.FilterIndex = 2;
            OpenFileDialog.RestoreDirectory = true;
            if (OpenFileDialog.ShowDialog() == DialogResult.OK)
            {
                //Clear UI Before this and Data
                ProgramState = State.LOADED;
                ClearElements();
                InitData();
                Animation = new AMTAnimation();
                OpenProject(OpenFileDialog.FileName);
            }
        }

        private void btnShowText_Click(object sender, EventArgs e)
        {
            if (ProgramState != State.READY)
                return;
            if (lbFrames.SelectedIndex == -1)
            {
                MessageBox.Show("You need to select a frame!");
                return;
            }
            if (lbFrames.SelectedItems.Count > 1)
            {
                MessageBox.Show("You cannot view more than one frame.");
                return;
            }
            FrameInfo InfoWindow = new FrameInfo(Animation.Actions[lbActions.SelectedIndex].Frames[lbFrames.SelectedIndex]);
            InfoWindow.Show();
        }

        private void btnShowPreview_Click(object sender, EventArgs e)
        {
            //When more than one frame, create duplicate action and view action
            if (ProgramState != State.READY)
                return;
            if (lbFrames.SelectedIndex == -1)
            {
                MessageBox.Show("You need to select a frame!");
                return;
            }
            if (lbFrames.SelectedItems.Count > 1)
            {
                MessageBox.Show("You cannot view more than one frame.");
                return;
            }
            if (Animation.Actions[lbActions.SelectedIndex].Frames[lbFrames.SelectedIndex].ActionRef == null)
            {
                FramePreview PreviewWindow = new FramePreview(ConvertBytesToImage(Frames[Animation.Actions[lbActions.SelectedIndex].Frames[lbFrames.SelectedIndex].FrameRef]));
                PreviewWindow.Show();
            }
            else
            {
                ActionPreview PreviewWindow = new ActionPreview(
                    Animation.Actions[Animation.Manifest.ActionFileName.IndexOf(Animation.Actions[lbActions.SelectedIndex].Frames[lbFrames.SelectedIndex].ActionRef)], Frames);
                PreviewWindow.Show();
            }
        }

        private void btnCreateAsNew_Click(object sender, EventArgs e)
        {
            if (ProgramState != State.READY)
                return;
            string PromptValue = InputPrompt.ShowDialog("Input New Action Name", "New Action");
            if (PromptValue == null)
                return;
            if (PromptValue == "")
            {
                MessageBox.Show("Input Empty!");
            }
            else if (Animation.Manifest.ActionFileName.Contains(PromptValue + ".act"))
            {
                MessageBox.Show("Action Already Exist!");
            }
            else
            {
                Animation.Manifest.ActionFileName.Add(PromptValue + ".act");
                Animation.Actions.Add(new AMTAction());
                Animation.Actions.Last().Name = PromptValue;
                foreach (object o in lbGifFrames.SelectedItems)
                {
                    Animation.Actions.Last().Frames.Add(new AMTFrame());
                    Animation.Actions.Last().Frames.Last().Delay = (int)nudDefaultDelay.Value;
                    Animation.Actions.Last().Frames.Last().FrameRef = lbGifFrames.Items.IndexOf(o);
                    Animation.Actions.Last().Frames.Last().MD5 = ImageMD5(ConvertBytesToImage(Frames[lbGifFrames.Items.IndexOf(o)]));
                    Animation.Actions.Last().Frames.Last().Tags.Add("null");
                }
                PopulateUI();
            }
        }

        private void lbActions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbActions.SelectedIndex == -1)
                return;
            lblCurrentAction.Text = Animation.Manifest.ActionFileName[lbActions.SelectedIndex];
            lbFrames.Items.Clear();
            foreach (AMTFrame frame in Animation.Actions[lbActions.SelectedIndex].Frames)
            {
                lbFrames.Items.Add(FrameToString(frame));
            }
        }

        private void btnAddToExisting_Click(object sender, EventArgs e)
        {
            if (ProgramState != State.READY)
                return;
            if (lbActions.SelectedIndex == -1)
            {
                MessageBox.Show("You need to select an action!");
                return;
            }
            foreach (object o in lbGifFrames.SelectedItems)
            {
                Animation.Actions[lbActions.SelectedIndex].Frames.Add(new AMTFrame());
                Animation.Actions[lbActions.SelectedIndex].Frames.Last().Delay = (int)nudDefaultDelay.Value;
                Animation.Actions[lbActions.SelectedIndex].Frames.Last().FrameRef = lbGifFrames.Items.IndexOf(o);
                Animation.Actions[lbActions.SelectedIndex].Frames.Last().MD5 = ImageMD5(ConvertBytesToImage(Frames[lbGifFrames.Items.IndexOf(o)]));
                Animation.Actions[lbActions.SelectedIndex].Frames.Last().Tags.Add("null");
            }
            PopulateFrames();
            lbFrames.SelectedIndex = lbFrames.Items.Count - 1;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (ProgramState != State.READY)
                return;
            Save();
        }

        private void btnMoveUp_Click(object sender, EventArgs e)
        {
            if (ProgramState != State.READY)
                return;
            if (lbFrames.SelectedIndex == -1)
            {
                MessageBox.Show("You need to select a frame!");
                return;
            }
            if (lbFrames.SelectedItems.Count > 1)
            {
                MessageBox.Show("You cannot move more than one frame.");
                return;
            }
            if (lbFrames.SelectedIndex == 0)
                return;
            else
            {
                //Swap
                int index = lbFrames.SelectedIndex;
                Swap<AMTFrame>(Animation.Actions[lbActions.SelectedIndex].Frames, lbFrames.SelectedIndex, lbFrames.SelectedIndex - 1);
                PopulateFrames();
                lbFrames.SelectedIndex = index - 1;
            }
        }

        private void btnMoveDown_Click(object sender, EventArgs e)
        {
            if (ProgramState != State.READY)
                return;
            if (lbFrames.SelectedIndex == -1)
            {
                MessageBox.Show("You need to select a frame!");
                return;
            }
            if (lbFrames.SelectedItems.Count > 1)
            {
                MessageBox.Show("You cannot move more than one frame.");
                return;
            }
            if (lbFrames.SelectedIndex == lbFrames.Items.Count - 1)
                return;
            else
            {
                //Swap
                int index = lbFrames.SelectedIndex;
                Swap<AMTFrame>(Animation.Actions[lbActions.SelectedIndex].Frames, lbFrames.SelectedIndex, lbFrames.SelectedIndex + 1);
                PopulateFrames();
                lbFrames.SelectedIndex = index + 1;
            }
        }

        private void btnDeleteFrame_Click(object sender, EventArgs e)
        {
            ListBox.SelectedObjectCollection Selected = lbFrames.SelectedItems;

            if (ProgramState != State.READY)
                return;
            if (lbFrames.SelectedIndex == -1)
            {
                MessageBox.Show("You need to select a frame!");
                return;
            }
            if (Selected.Count >= lbFrames.Items.Count)
            {
                MessageBox.Show("Cannot delete all frame in action.");
                return;
            }
            /*if (lbFrames.SelectedItems.Count > 1)
            {
                MessageBox.Show("Cannot delete more than one frame at a time.");
                return;
            }*/
            if (MessageBox.Show("Do you want to delete this frame?", "Delete " +
                Animation.Manifest.ActionFileName[lbActions.SelectedIndex], MessageBoxButtons.YesNo) == DialogResult.No)
            {
                return;
            }
            for (int i = lbFrames.SelectedItems.Count - 1; i >= 0; i--)
            {
                Animation.Actions[lbActions.SelectedIndex].Frames.RemoveAt(lbFrames.Items.IndexOf(lbFrames.SelectedItems[i]));
                lbFrames.SelectedItems.Remove(lbFrames.SelectedItems[i]);
            }
            //int selectefFrame = lbFrames.SelectedIndex;
            //Animation.Actions[lbActions.SelectedIndex].Frames.RemoveAt(lbFrames.SelectedIndex);
            PopulateFrames();
           // if(selectefFrame != 0)
                //lbFrames.SelectedIndex = selectefFrame - 1;
            //else
                //lbFrames.SelectedIndex = selectefFrame;
        }

        private void btnChangeDelay_Click(object sender, EventArgs e)
        {
            if (ProgramState != State.READY)
                return;
            if (lbFrames.SelectedIndex == -1)
            {
                MessageBox.Show("You need to select a frame!");
                return;
            }
            string PromptValue = InputPrompt.ShowDialog("Input a new time in milliseconds", "Edit time", 
                Animation.Actions[lbActions.SelectedIndex].Frames[lbFrames.SelectedIndex].Delay.ToString());
            //User Cancel Action
            if (PromptValue == null)
                return;
            if (PromptValue == "")
            {
                MessageBox.Show("Input Empty!");
            }
            else
            {
                foreach (object o in lbFrames.SelectedItems)
                {
                    Animation.Actions[lbActions.SelectedIndex].Frames[lbFrames.Items.IndexOf(o)].Delay = Convert.ToInt32(GetNumbers(PromptValue));
                }
            }
            PopulateFrames();
        }

        private void btnEditTag_Click(object sender, EventArgs e)
        {
            string tags = "";
            if (ProgramState != State.READY)
                return;
            if (lbFrames.SelectedIndex == -1)
            {
                MessageBox.Show("You need to select a frame!");
                return;
            }
            foreach (string s in Animation.Actions[lbActions.SelectedIndex].Frames[lbFrames.SelectedIndex].Tags)
            {
                tags += (s + ",");
            }
            string PromptValue = InputPrompt.ShowDialog("Input tags, seperated by comma", "Edit Tags",
                tags);
            //User Cancel Action
            if (PromptValue == null)
                return;
            foreach (object o in lbFrames.SelectedItems)
            {
                Animation.Actions[lbActions.SelectedIndex].Frames[lbFrames.Items.IndexOf(o)].Tags.Clear();
                foreach (string s in PromptValue.Split(','))
                {
                    if (s != "")
                        Animation.Actions[lbActions.SelectedIndex].Frames[lbFrames.Items.IndexOf(o)].Tags.Add(s);
                }
            }
            PopulateFrames();
            //No Previous Selection Recovery
        }

        private void btnDeleteAction_Click(object sender, EventArgs e)
        {
            int index = lbActions.SelectedIndex;
            if (ProgramState != State.READY)
                return;
            if (lbActions.SelectedIndex == -1)
            {
                MessageBox.Show("You need to select a action!");
                return;
            }
            if(Animation.Actions[lbActions.SelectedIndex].Name.Equals("null"))
            {
                MessageBox.Show("Cannot delete null action.");
                return;
            }
            if (lbActions.Items.Count == 1)
            {
                MessageBox.Show("Cannot Delete Only action in animation.");
                return;
            }
            //Confirm??
            if (MessageBox.Show("Do you want to delete this action?", "Delete " +
                Animation.Manifest.ActionFileName[lbActions.SelectedIndex], MessageBoxButtons.YesNo) == DialogResult.No)
            {
                return;
            }
            //Update Manifest
            //Delete action in Animation
            //Delete file
            //Update UI
            File.Delete(Path.Combine(WorkingDir, Animation.Manifest.ActionFileName[lbActions.SelectedIndex]));
            Animation.Manifest.ActionFileName.RemoveAt(lbActions.SelectedIndex);
            Animation.Actions.RemoveAt(lbActions.SelectedIndex);
            PopulateUI();
            Save();
            lbActions.SelectedIndex = index - 1;
        }

        private void btnPlayAction_Click(object sender, EventArgs e)
        {
            if (ProgramState != State.READY)
                return;
            if (lbActions.SelectedIndex == -1)
            {
                MessageBox.Show("You need to select a action!");
                return;
            }
            ActionPreview PreviewWindow = new ActionPreview(ExpandFrame(Animation.Actions[lbActions.SelectedIndex]), Frames);
            PreviewWindow.Show();
        }

        private void btnFrameRef_Click(object sender, EventArgs e)
        {
            int index = lbFrames.SelectedIndex;
            if (ProgramState != State.READY)
                return;
            string FileName = (string)Selection_Prompt<string>.ShowDialog("Select a action you want to add as reference", "Action Selection", Animation.Manifest.ActionFileName);
            if (FileName == null)
                return;
            //Cannot Reference its self
            if (FileName == Animation.Manifest.ActionFileName[lbActions.SelectedIndex])
            {
                MessageBox.Show("Cannot reference yourself.", "Critical Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            //lblDebug.Text = FileName;
            Animation.Actions[lbActions.SelectedIndex].Frames.Add(new AMTFrame());
            Animation.Actions[lbActions.SelectedIndex].Frames.Last().Delay = (int)nudDefaultDelay.Value;
            Animation.Actions[lbActions.SelectedIndex].Frames.Last().ActionRef = FileName;
            Animation.Actions[lbActions.SelectedIndex].Frames.Last().MD5 = null;
            Animation.Actions[lbActions.SelectedIndex].Frames.Last().Tags.Add("null");
            PopulateFrames();
            lbFrames.SelectedIndex = lbFrames.Items.Count - 1;
        }

        private void tAutoSave_Tick(object sender, EventArgs e)
        {
            if (ProgramState != State.READY)
                return;
            Save();
        }
    }
}
