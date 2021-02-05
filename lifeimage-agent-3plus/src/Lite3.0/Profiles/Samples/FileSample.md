The File Sample profile demonstrates receving and sending files using the FileConnector labeled as simply "File" in the profile.  You can use dcmsend to receive a file to inpath, and you can place files in scanpath and they will be sent to cloud and then moved to outpath.

Example usage of dcmsend:

1) replace path below with your path to some dicom files

2) dcmsend localhost 11113 /Users/sbuck/Public/NSCLC\ Radiogenomics-Demo/ +sd +r

Example usage of scanpath:

1) Copy files to scanpath and observe the files appearing in Cloud and then moving to outpath.

