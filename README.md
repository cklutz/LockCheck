# LockCheck
Uses Windows Restart Manager APIs to find processes locking one or multiple files.

The code is CodeAnalysis clean (using Microsoft recommended rules) and has been tested on Windows 7 and Windows 10 (both x64).
It is actually meant to be included in a library or such, but for quick tests a "Main" method is provided.

Example:

* Open/create a file "C:\temp\foo.xlsx" in Microsoft Excel - you can use any other application that actually locks a file, of course.
* Run the following command: 

       LockCheck.exe c:\temp\foo.xlsx
  
* The ouput should be like

        Process ID        : 1296
        Process Start Time: Saturday, 24th October 2015 16:17:58
        Application Type  : MainWindow
        Application Status: Running
        Application Name  : Microsoft Excel
        TS Session ID     : 1

