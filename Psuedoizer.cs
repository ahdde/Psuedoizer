using System;
using System.Linq;
using System.Resources;
using System.IO;
using System.Collections;
using System.Text;
using System.Globalization;
using System.Threading;

namespace Pseudo.Globalization
{
    ///Takes an English resource file (resx) and creates an artificial");/
    ///but still readable Euro-like language to exercise your i18n code");
    ///without a formal translation.");
    class Psuedoizer
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("Psuedoizer: Adapted from MSDN BugSlayer 2004-Apr i18n Article.");
            if (args.Length < 2)
            {
                Console.WriteLine("Purpose: Takes an English resource file (resx) and creates an artificial");
                Console.WriteLine("         but still readable Euro-like language to exercise your i18n code");
                Console.WriteLine("         without a formal translation.");
                Console.WriteLine();
                Console.WriteLine("Psuedoizer.exe infile outfile [/b]");
                Console.WriteLine("    Example:");
                Console.WriteLine("    Psuedoizer.exe strings.en.resx strings.ja-JP.resx");
                Console.WriteLine("    /b - Include blank resources");
                Console.WriteLine();
                Console.WriteLine("Alternative: use a directory and a language code");
                Console.WriteLine("Psuedoizer.exe dir lang [/b]");
                Console.WriteLine("    Example:");
                Console.WriteLine("    Psuedoizer.exe . ja-JP");
                Console.WriteLine("    /b - Include blank resources");
                System.Environment.Exit(1);
            }

            string fileNameOrDirectory = args[0];
            string fileSaveNameOrLangCode = args[1];
            bool includeBlankResources = (args.Length >= 3 && args[2] == "/b");

            try
            {
                if (Directory.Exists(fileNameOrDirectory))
                {
                    TranslateMultipleFiles(fileNameOrDirectory, fileSaveNameOrLangCode, includeBlankResources);
                }
                else
                {
                    TranslateSingleFile(fileNameOrDirectory, fileSaveNameOrLangCode, includeBlankResources);
                }
            }
            catch (Exception e)
            {
                Console.Write(e.ToString());
                System.Environment.Exit(1);
            }
        }

        private static readonly CultureInfo[] allCultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);
        private static void TranslateMultipleFiles(string directory, string langCode, bool includeBlankResources)
        {
            foreach (var file in Directory.GetFiles(directory, "*.resx"))
            {
                // Check if it's the neutral resource file
                var fileName = Path.GetFileNameWithoutExtension(file);
                var extension = Path.GetExtension(fileName).Trim(' ', '.').ToLower();
                if (string.IsNullOrEmpty(extension) || (!extension.Equals(langCode, StringComparison.OrdinalIgnoreCase) && allCultures.All(c => c.Name.ToLower() != extension && c.TwoLetterISOLanguageName.ToLower() != extension)))
                {
                    TranslateSingleFile(file, string.Format("{0}\\{1}.{2}.resx", directory, fileName, langCode), includeBlankResources);
                }
            }

            foreach (var subDir in Directory.GetDirectories(directory))
            {
                TranslateMultipleFiles(subDir, langCode, includeBlankResources);
            }
        }

        private static void TranslateSingleFile(string fileName, string fileSaveName, bool includeBlankResources)
        {
            if (fileName == null) throw new ArgumentNullException(nameof(fileName));
            if (fileSaveName == null) throw new ArgumentNullException(nameof(fileSaveName));

            if (!ResourceFileNeedsTranslation(fileName, fileSaveName))
                return;

            // Open the input file.
            ResXResourceReader reader = new ResXResourceReader(fileName);
            try
            {
                // Get the enumerator.  If this throws an ArguementException
                // it means the file is not a .RESX file.
                IDictionaryEnumerator enumerator = reader.GetEnumerator();
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine("WARNING: could not parse " + fileName);
                Console.WriteLine("         " + ex.Message);
                return;
            }

            // Allocate the list for this instance.
            SortedList textResourcesList = new SortedList();

            // Run through the file looking for only true text related
            // properties and only those with values set.
            foreach (DictionaryEntry dic in reader)
            {
                // Only consider this entry if the value is something.
                if (null != dic.Value)
                {
                    // Is this a System.String.
                    if ("System.String" == dic.Value.GetType().ToString())
                    {
                        String KeyString = dic.Key.ToString();

                        // Make sure the key name does not start with the
                        // "$" or ">>" meta characters and is not an empty
                        // string (or we're explicitly including empty strings).
                        if ((false == KeyString.StartsWith(">>")) &&
                            (false == KeyString.StartsWith("$")) &&
                            (includeBlankResources || "" != dic.Value.ToString()))
                        {
                            // We've got a winner.
                            textResourcesList.Add(dic.Key, dic.Value);
                        }

                        // Special case the Windows Form "$this.Text" or
                        // I don't get the form titles.
                        if (0 == String.Compare(KeyString, "$this.Text"))
                        {
                            textResourcesList.Add(dic.Key, dic.Value);
                        }

                    }
                }
            }

            // It's entirely possible that there are no text strings in the
            // .ResX file.
            if (textResourcesList.Count > 0)
            {
                if (null != fileSaveName)
                {
                    if (File.Exists(fileSaveName))
                    {
                        if( !TryDeleteFile(fileSaveName))
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(2));
                            if(!TryDeleteFile(fileSaveName))
                                throw new Exception($"File {fileSaveName} is still locked.");
                        }
                    }

                    // Create the new file.
                    ResXResourceWriter writer =
                        new ResXResourceWriter(fileSaveName);

                    foreach (DictionaryEntry textdic in textResourcesList)
                    {
                        writer.AddResource(textdic.Key.ToString(), Psuedoizer.ConvertToFakeInternationalized(textdic.Value.ToString()));
                    }

                    writer.Generate();
                    writer.Close();
                    Console.WriteLine(String.Format("{0}: converted {1} text resource(s).", fileName, textResourcesList.Count));
                }
            }
            else
            {
                Console.WriteLine("WARNING: No text resources found in " + fileName);
            }
        }

        private static bool ResourceFileNeedsTranslation(string fileName, string fileSaveName)
        {
            if (!File.Exists(fileSaveName))
                return true;

            if (System.IO.File.GetLastWriteTime(fileSaveName) <= System.IO.File.GetLastWriteTime(fileName)) 
                return true;

            Console.WriteLine($"Skipped {fileName}");
            return false;
        }

        private static bool TryDeleteFile(string file)
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                Console.WriteLine($"file is locked: {file}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Converts a string to a pseudo internationized string.
        /// </summary>
        /// <remarks>
        /// Primarily for latin based languages.  This will need updating to
        /// work with Eastern languages.
        /// </remarks>
        /// <param name="inputString">
        /// The string to use as a base.
        /// </param>
        /// <returns>
        /// A longer and twiddled string.
        /// </returns>
        public static String ConvertToFakeInternationalized(String inputString)
        {

            //check if the input string is a http or https link... if it is, do not localize
            if (inputString.Contains("http://") || inputString.Contains("https://"))
            {
                return inputString;
            }


            // Calculate the extra space necessary for pseudo
            // internationalization.  The rules, according to "Developing
            // International Software" is that < 10  characters you should grow
            // by 400% while >= 10 characters should grow by 30%.

            int OrigLen = inputString.Length;
            int PseudoLen = 0;
            if (OrigLen < 10)
            {
                PseudoLen = (OrigLen * 4) + OrigLen;
            }
            else
            {
                PseudoLen = ((int)(OrigLen * 0.3)) + OrigLen;
            }

            StringBuilder sb = new StringBuilder(PseudoLen);

            // The pseudo string will always start with a "[" and end
            // with a "]" so you can tell if strings are not built
            // correctly in the UI.
            sb.Append("[");

            bool waitingForEndBrace = false;
            bool waitingForGreaterThan = false;
            foreach (Char currChar in inputString)
            {
                switch (currChar)
                {
                    case '{':
                        waitingForEndBrace = true;
                        break;
                    case '}':
                        waitingForEndBrace = false;
                        break;
                    case '<':
                        waitingForGreaterThan = true;
                        break;
                    case '>':
                        waitingForGreaterThan = false;
                        break;
                }
                if (waitingForEndBrace || waitingForGreaterThan)
                {
                    sb.Append(currChar);
                    continue;
                }
                switch (currChar)
                {
                    case 'A':
                        sb.Append('Å');
                        break;
                    case 'B':
                        sb.Append('ß');
                        break;
                    case 'C':
                        sb.Append('C');
                        break;
                    case 'D':
                        sb.Append('Đ');
                        break;
                    case 'E':
                        sb.Append('Ē');
                        break;
                    case 'F':
                        sb.Append('F');
                        break;
                    case 'G':
                        sb.Append('Ğ');
                        break;
                    case 'H':
                        sb.Append('Ħ');
                        break;
                    case 'I':
                        sb.Append('Ĩ');
                        break;
                    case 'J':
                        sb.Append('Ĵ');
                        break;
                    case 'K':
                        sb.Append('Ķ');
                        break;
                    case 'L':
                        sb.Append('Ŀ');
                        break;
                    case 'M':
                        sb.Append('M');
                        break;
                    case 'N':
                        sb.Append('Ń');
                        break;
                    case 'O':
                        sb.Append('Ø');
                        break;
                    case 'P':
                        sb.Append('P');
                        break;
                    case 'Q':
                        sb.Append('Q');
                        break;
                    case 'R':
                        sb.Append('Ŗ');
                        break;
                    case 'S':
                        sb.Append('Ŝ');
                        break;
                    case 'T':
                        sb.Append('Ŧ');
                        break;
                    case 'U':
                        sb.Append('Ů');
                        break;
                    case 'V':
                        sb.Append('V');
                        break;
                    case 'W':
                        sb.Append('Ŵ');
                        break;
                    case 'X':
                        sb.Append('X');
                        break;
                    case 'Y':
                        sb.Append('Ÿ');
                        break;
                    case 'Z':
                        sb.Append('Ż');
                        break;


                    case 'a':
                        sb.Append('ä');
                        break;
                    case 'b':
                        sb.Append('þ');
                        break;
                    case 'c':
                        sb.Append('č');
                        break;
                    case 'd':
                        sb.Append('đ');
                        break;
                    case 'e':
                        sb.Append('ę');
                        break;
                    case 'f':
                        sb.Append('ƒ');
                        break;
                    case 'g':
                        sb.Append('ģ');
                        break;
                    case 'h':
                        sb.Append('ĥ');
                        break;
                    case 'i':
                        sb.Append('į');
                        break;
                    case 'j':
                        sb.Append('ĵ');
                        break;
                    case 'k':
                        sb.Append('ĸ');
                        break;
                    case 'l':
                        sb.Append('ľ');
                        break;
                    case 'm':
                        sb.Append('m');
                        break;
                    case 'n':
                        sb.Append('ŉ');
                        break;
                    case 'o':
                        sb.Append('ő');
                        break;
                    case 'p':
                        sb.Append('p');
                        break;
                    case 'q':
                        sb.Append('q');
                        break;
                    case 'r':
                        sb.Append('ř');
                        break;
                    case 's':
                        sb.Append('ş');
                        break;
                    case 't':
                        sb.Append('ŧ');
                        break;
                    case 'u':
                        sb.Append('ū');
                        break;
                    case 'v':
                        sb.Append('v');
                        break;
                    case 'w':
                        sb.Append('ŵ');
                        break;
                    case 'x':
                        sb.Append('χ');
                        break;
                    case 'y':
                        sb.Append('y');
                        break;
                    case 'z':
                        sb.Append('ž');
                        break;
                    default:
                        sb.Append(currChar);
                        break;
                }
            }

            // Poke on extra text to fill out the string.
            const String PadStr = " !!!";
            int PadCount = (PseudoLen - OrigLen - 2) / PadStr.Length;
            if (PadCount < 2)
            {
                PadCount = 2;
            }

            for (int x = 0; x < PadCount; x++)
            {
                sb.Append(PadStr);
            }

            // Pop on the trailing "]"
            sb.Append("]");

            return (sb.ToString());
        }
    }

}
