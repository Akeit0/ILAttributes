using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace ILAttributes.CodeGen
{

   
    public static class FileUtils
    {
        public static string GetCallerProjectPath([CallerFilePath]string s=null)
        {
            if (s == null) return s;
            var dirs= s.Split(Path.DirectorySeparatorChar);;
            var last = dirs.Length -1;
            while (dirs[last--]!="ILAttributes")
            {
            }

            if (dirs[last-1] == "PackageCache")
            {
                last -= 3;
            }

            return string.Join(Path.DirectorySeparatorChar, dirs.Take(last));
        }
        public static string GetCorePathWrittenPath()
        {
            var path = GetCallerProjectPath();
            var writePath = path + Path.DirectorySeparatorChar + "corepath.txt";
            return writePath;
        }
    }
    
    
    
    [InitializeOnLoad]
    public  static class CoreLib
    {
        static CoreLib()
        {
            Location = null;
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
            CompilationPipeline.compilationStarted += LogCore;
        }

        public static string Location;

        static void OnDomainUnload(object _, EventArgs __)
        {
            AppDomain.CurrentDomain.DomainUnload-= OnDomainUnload;
            CompilationPipeline.compilationStarted -= LogCore;
        }
        static void LogCore(object _)
        {
            if(Location!=null)return;
            Location = typeof(int).Module.Assembly.Location;
            //Debug.Log(Location);
            if(string.IsNullOrEmpty(Location))return;
            var path = FileUtils.GetCallerProjectPath();
            var writePath = path + Path.DirectorySeparatorChar + "corepath.txt";
           // Debug.Log(writePath);
            File.WriteAllText(writePath,Location);
        }
    }
    
}