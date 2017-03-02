//
// Copyright 2017 Ray Li
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;


namespace build_file_db
{
    class Program
    {
        static void process_dir(string dir)
        {
            try
            {
                var iter_dirs = Directory.EnumerateDirectories(dir);
                foreach (string subdir in iter_dirs)
                {
                    process_dir(subdir);
                }

                string db_filename      = dir + @"\.filedb";
                var    file_db          = new Dictionary<string, filedb.file_info_type>();
                bool   file_db_exists   = false;
                bool   file_db_modified = false;

                if (File.Exists(db_filename))
                {
                    // Load old file db info
                    //
                    IFormatter formatter = new BinaryFormatter();
                    Stream stream = new FileStream(db_filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                    file_db = (Dictionary<string, filedb.file_info_type>) formatter.Deserialize(stream);
                    stream.Close();
                    file_db_exists = true;
                }
                else
                {
                    // Create a new file db
                    //
                    file_db_modified = true;
                }


                SHA256 hasher = SHA256Managed.Create();
                byte[] hash_value;

                // survey files in the directory
                //
                var found_files = new HashSet<string>();

                var iter_files = Directory.EnumerateFiles(dir);
                foreach (string file in iter_files)
                {
                    string file_basename = Path.GetFileName(file);
                    var finfo = new FileInfo(file);

                    if (file_basename.Equals(".filedb", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    found_files.Add(file_basename);

                    // If there is a size/time mismatch between the
                    // current file info and the file info
                    // stored in the database, the file hash
                    // needs to be computed
                    //
                    bool update_file_db_entry = false;
                    if (file_db.ContainsKey(file_basename))
                    {
                        if ((file_db[file_basename].mtime != finfo.LastWriteTimeUtc) ||
                             (file_db[file_basename].size != finfo.Length))
                        {
                            Console.WriteLine(file + " has changed");
                            update_file_db_entry = true;
                        }
                    }
                    else
                    {
                        Console.WriteLine(file + " does not exist in db");
                        update_file_db_entry = true;
                    }

                    if (update_file_db_entry)
                    {
                        // compute file hash
                        //
                        using (FileStream fstream = File.OpenRead(file))
                        {
                            fstream.Position = 0;
                            hash_value = hasher.ComputeHash(fstream);
                        }

                        file_db[file_basename] = new filedb.file_info_type { size = finfo.Length, mtime = finfo.LastWriteTimeUtc, hash = hash_value };
                        file_db_modified = true;
                        Console.WriteLine("file db entry for " + file_basename + " updated");
                    }
                }

                // Delete any data records for files in the database
                // that are not currently in the directory
                //
                var to_be_removed = new List<string>();
                foreach (string file in file_db.Keys)
                {
                    if (!found_files.Contains(file))
                    {
                        to_be_removed.Add(file);
                    }
                }

                foreach (string file in to_be_removed)
                {
                    file_db.Remove(file);
                    file_db_modified = true;
                    Console.WriteLine("file db entry for " + file + " removed");
                }

                if (file_db_modified)
                {
                    if (file_db.Count > 0)
                    {
                        // Serialize file_db
                        //
                        IFormatter formatter = new BinaryFormatter();

                        // If file already exists, unhide it
                        //
                        if (File.Exists(db_filename))
                        {
                            File.SetAttributes(db_filename, File.GetAttributes(db_filename) & ~FileAttributes.Hidden);
                        }

                        // Create/update file_db
                        //
                        Stream stream = new FileStream(
                                                        db_filename
                                                      , FileMode.Create
                                                      , FileAccess.Write
                                                      , FileShare.None
                                                      );
                        formatter.Serialize(stream, file_db);
                        stream.Close();

                        // Hide file_db file
                        //
                        File.SetAttributes(db_filename, File.GetAttributes(db_filename) | FileAttributes.Hidden);
                        if (file_db_exists)
                        {
                            Console.WriteLine(db_filename + " updated");
                        }
                        else
                        {
                            Console.WriteLine(db_filename + " created");
                        }
                    }
                    else
                    {
                        File.Delete(db_filename);
                        if (file_db_exists)
                        {
                            Console.WriteLine(db_filename + " removed");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("no updates to " + db_filename + " required");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }


        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.Unicode;
            foreach (string arg in args)
            {
                process_dir(arg);
            }
        }
    }
}
