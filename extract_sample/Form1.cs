using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Ionic.Zip;

namespace extract_sample
{
  public partial class Form1 : Form
  {
    private List<string> compressedfiles = new List<string>();

    private delegate void delegateProgressBar(Int32 v);
    private delegate void threadTerminated(Int32 c);
    private delegate void delegateUpdateLabel(string s);

    public Form1()
    {
      InitializeComponent();
      button1.Enabled = false;
    }

    private void Form1_DragEnter(object sender, DragEventArgs e)
    {
      if (e.Data.GetDataPresent(DataFormats.FileDrop))
        e.Effect = DragDropEffects.All;
    }

    private void Form1_DragDrop(object sender, DragEventArgs e)
    {
      string[] s = (string[])e.Data.GetData(DataFormats.FileDrop, false);
      int i;
      for (i = 0; i < s.Length; i++) compressedfiles.Add(s[i]);
      if (compressedfiles.Count > 0) button1.Enabled = true;
    }

    private void button1_Click(object sender, EventArgs e)
    {
      if (compressedfiles.Count == 0) return;
      progressBar1.Visible = true;
      System.Threading.Thread extract_thread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(extracts));
      extract_thread.Start(compressedfiles);
    }

    private void extracts(Object arg)
    {
      List<string> compressedfiles = (List<string>)arg;

      foreach (string source in compressedfiles)
      {
        string desitination = Path.GetDirectoryName(source);

        if (source.EndsWith(".tgz") == true || source.EndsWith(".tar.gz") == true)
        {
          using (GZipInputStream tgz = new GZipInputStream(new FileStream(source, FileMode.Open,FileAccess.Read)))
          {
            using (TarInputStream tar = new TarInputStream(tgz))
            {
              extract_tar(tar, desitination);
              tar.Close();
            }
            tgz.Close();
          }
        }
        else if (source.EndsWith(".tar"))
        {
/** for not hasty man
          using (ICSharpCode.SharpZipLib.Tar.TarArchive ta = 
            TarArchive.CreateInputTarArchive(new TarInputStream(new FileStream(source, FileMode.Open, FileAccess.Read))))
          {
            ta.ProgressMessageEvent += new ProgressMessageHandler(tar_ProgressMessageEvent);
            ta.ExtractContents(desitination);
          }
*/ 
          using (TarInputStream tar = new TarInputStream(new FileStream(source, FileMode.Open, FileAccess.Read)))
          {
            extract_tar(tar, desitination);
            tar.Close();
          }
        }
#if _USE_ICONIC_
/** for not hasty man
        using (Ionic.Zip.ZipFile zip = Ionic.Zip.ZipFile.Read(source))
        {
          foreach (Ionic.Zip.ZipEntry entry in zip)
          {
            entry.Extract(desitination, ExtractExistingFileAction.OverwriteSilently);
          }
        }
*/
        using (Ionic.Zip.ZipFile zip = Ionic.Zip.ZipFile.Read(source))
        {
          zip.ExtractProgress += new EventHandler<Ionic.Zip.ExtractProgressEventArgs>(extract_zip);
          foreach (Ionic.Zip.ZipEntry entry in zip)
          {
            Invoke(new delegateUpdateLabel((string s) => { this.label1.Text = s; }), new Object[] { entry.FileName + " extracting ..." });
            entry.Extract(desitination, ExtractExistingFileAction.OverwriteSilently);
          }
        }
#else 
        else if (source.EndsWith(".zip"))
        {
          //and so on... ** but! ** see. http://community.sharpdevelop.net/forums/t/11466.aspx
        }
#endif
      }
      Invoke(new threadTerminated(extract_terminated), compressedfiles.Count);
    }

    void tar_ProgressMessageEvent(TarArchive archive, TarEntry entry, string message)
    {
      Invoke(new delegateUpdateLabel((string s) => { this.label2.Text = s; }), new Object[] { entry.Name + " extracting ..." });
    }

    /**
     * extract tar file 
     * 
     */
    private void extract_tar(TarInputStream tar, string desitination)
    {
      delegateProgressBar increment_bar =
       new delegateProgressBar((int v) => { Cursor.Current = Cursors.WaitCursor; progressBar1.Value = v; });

      ICSharpCode.SharpZipLib.Tar.TarEntry entry = tar.GetNextEntry();

      while (entry != null)
      {
        if (entry.IsDirectory == true)
        {
          if  (Directory.Exists(desitination + "\\" + entry.Name)) Directory.Delete(desitination + "\\" + entry.Name, true);
          if (!Directory.Exists(desitination + "\\" + entry.Name)) Directory.CreateDirectory(desitination + "\\" + entry.Name);
          entry = tar.GetNextEntry();
          continue;
        }

        Invoke(new delegateUpdateLabel((string s) => { this.label2.Text = s; }), new Object[] { entry.Name + " extracting ..." });

        Invoke(new delegateProgressBar((Int32 v) => { progressBar1.Value = 0; progressBar1.Minimum = 0; progressBar1.Maximum = v; }),
          new Object[] { (Int32.MaxValue < entry.Size) ? (Int32)(entry.Size / 65536) : (Int32)entry.Size });

        using (FileStream dest = new FileStream(desitination + "\\" + entry.Name, FileMode.Create, FileAccess.Write))
        {
          Int32 count = 0;
          Int32 write_total = 0;
          byte[] buffer = new byte[32768];

          using (BinaryWriter br = new BinaryWriter(dest))
          {
            while ((count = tar.Read(buffer, 0, 32768)) > 0)
            {
              br.Write(buffer, 0, count);
              write_total += count;
              if (Int32.MaxValue < entry.Size)
              {
                Object[] inc_arg = { (Int32)write_total / 65536 };
                Invoke(increment_bar, inc_arg);
              }
              else
              {
                Object[] inc_arg = { write_total };
                Invoke(increment_bar, inc_arg);
              }
            }
            br.Flush();
            br.Close();
          }
          dest.Close();
        }
        entry = tar.GetNextEntry();
      }
    }

    /**
     * extract zip file 
     * 
     */
    private void extract_zip(object sender, Ionic.Zip.ExtractProgressEventArgs e)
    {
      if (e.EventType == Ionic.Zip.ZipProgressEventType.Extracting_BeforeExtractEntry)
      {
        Int32 total_size = 0;
        if (Int32.MaxValue < e.CurrentEntry.UncompressedSize)
        {
          total_size = (Int32)(e.CurrentEntry.UncompressedSize / 65536);
        }
        else
        {
          total_size = (Int32)e.CurrentEntry.UncompressedSize;
        }
        Invoke(new delegateProgressBar((Int32 v) => { progressBar1.Value = 0; progressBar1.Minimum = 0; progressBar1.Maximum = v; }), new Object[] { total_size });
      }
      else if (e.EventType == Ionic.Zip.ZipProgressEventType.Extracting_EntryBytesWritten)
      {
        delegateProgressBar increment_bar =
          new delegateProgressBar((int v) => { Cursor.Current = Cursors.WaitCursor; progressBar1.Value = v; });

        if (Int32.MaxValue < e.CurrentEntry.UncompressedSize)
        {
          Object[] inc_arg = { (Int32)e.BytesTransferred / 65536 };
          Invoke(increment_bar, inc_arg);
        }
        else
        {
          Object[] inc_arg = { (Int32)e.BytesTransferred };
          Invoke(increment_bar, inc_arg);
        }
      }
      else
      {
        // nothing todo
      }
    }

    private void extract_terminated(Int32 count)
    {
      compressedfiles.Clear();
      button1.Enabled = false;
      progressBar1.Visible = false;
      label1.Text = "アーカイブをドロップしてください";
      label2.Text = "";
    }

  }
}
