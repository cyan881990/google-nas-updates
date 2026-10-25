using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
class GenerateIcon {
    static int Main(string[] args) {
        int[] sizes={16,20,24,32,40,48,64,128,256}; var images=new List<byte[]>();
        foreach(int size in sizes) using(var bitmap=GoogleNas.SuiteGlyph.Render(args[1]=="Server",size)) {
            using(var stream=new MemoryStream()) { bitmap.Save(stream,ImageFormat.Png); images.Add(stream.ToArray()); }
        }

        using(var writer=new BinaryWriter(File.Create(args[0]))) {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++) {
                writer.Write((byte)(sizes[i]==256?0:sizes[i])); writer.Write((byte)(sizes[i]==256?0:sizes[i])); writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(images[i].Length); writer.Write(offset); offset+=images[i].Length;
            }
            foreach(byte[] bytes in images) writer.Write(bytes);
        }
        return 0;
    }
}
