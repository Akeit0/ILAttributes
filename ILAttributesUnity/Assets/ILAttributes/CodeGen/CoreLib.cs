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
        
        public static string GetCorePathWrittenPath()
        {
            return Path.Combine(Environment.CurrentDirectory, "corepath.txt");
        }
    }
    
    
    
    [InitializeOnLoad]
    public  static class CoreLib
    {
        static CoreLib()
        {
           // Debug.Log("CurrentDirectory "+Environment.CurrentDirectory);
            Location = typeof(int).Module.Assembly.Location;
            File.WriteAllText(FileUtils.GetCorePathWrittenPath(),Location);
            Location = null;
          //  AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
           // CompilationPipeline.compilationStarted += LogCore;
        }

        public static string Location;

        // static void OnDomainUnload(object _, EventArgs __)
        // {
        //     AppDomain.CurrentDomain.DomainUnload-= OnDomainUnload;
        //     CompilationPipeline.compilationStarted -= LogCore;
        // }
        // static void LogCore(object _)
        // {
        //    // Debug.Log("Current "+Environment.CurrentDirectory);
        //     if(Location!=null)return;
        //     Location = typeof(int).Module.Assembly.Location;
        //     //Debug.Log(Location);
        //     if(string.IsNullOrEmpty(Location))return;
        //     var path = FileUtils.GetCorePathWrittenPath();
        //    // Debug.Log(writePath);
        //     File.WriteAllText(path,Location);
        // }
    }
    
}