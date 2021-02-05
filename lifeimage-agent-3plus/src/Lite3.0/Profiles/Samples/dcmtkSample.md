# dcmtk Sample

The dcmtk sample profile is to be used with dcmsend and dcmrecv tools to demonstrate an end-to-end transfer between two or more LifeImage Cloud organizational entities.  The below examples will send to the second cloud accound and receive from the first cloud account, demonstrating the use of Life Image Transfer Exchange bi-directional capabilities.

Example usage of dcmrecv:

1) CD to directory where you want files to be saved

2) dcmrecv --verbose 11112 --config-file ~/dcmtk/dcmnet/etc/storescp.cfg default -uca
2a) D:\DevTools\dcmtk-3.6.3-win64-dynamic\\bin\dcmrecv --debug 11112 --config-file D:\DevTools\dcmtk-3.6.3-win64-dynamic\\etc\dcmtk\storescp.cfg default -uca

Example usage of dcmsend:

1) replace path below with your path to some dicom files

2) dcmsend localhost 11115 /Users/sbuck/Public +sd +r -v +v -nh -aec LITEDCMSEND

# dcmqrscp

The dcmqrscp tool implements a simple image archive. It supports image storage, retrieval and image attribute querying.
dcmqrscp usage example:

1. Follow the instructions to configure your storage area as outlined in the https://support.dcmtk.org/docs/file_dcmqrset.html link.
You will be required to modify dcmqrscp.cfg example file that comes with the dcmtk install. You will have to:

- Set AETable entry to point to your storage directory, e.g.:
VICS_PACS /Users/vnordenberg/pacs/VICS_PACS RW (9, 1024mb) ANY

- Set HostTable entry to specify host/port for your AE, e.g.:
vic1 = (VIC1, localhost, 6666)

2. Start dcmqrscp server with the command:
dcmqrscp -v -d -c <PATH_TO_YOUR_CONFIGURATION_FILE>/dcmqrscp.cfg <PORT>, where port
corresponds to the port defined within 'HostTable' section of the dcmqrscp.cfg file, e.g.,
dcmqrscp -v -d -c /Users/vnordenberg/pacs/dcmqrscp.cfg 6666

3. Now you can store dicom image to your PACS using the command:
storescu --propose-lossless -v --aetitle <YOUR_AE_TITLE> --call <YOUR_STORAGE_AREA> <HOST> <PORT> <IMAGE_FILE>, for example:
storescu --propose-lossless -v --aetitle VIC1 --call VICS_PACS localhost 6666 ~/Desktop/test1.dcm

4. And you can query PACS via findscu command, for example:
findscu -S -k "(0008,0052)=STUDY" -k "(0008,0020)=20110920" localhost 6666 -aec VICS_PACS, which allows you
to find studies performed on a certain date (September 20th, 2011).

## shb example use of dcmqrscp

dcmqrscp -v -c ~/dicom/db/dcmqrscp.cfg 11120

storescu -v +sd +r --call DCMTK stephens-mbp.lifeimage.lan 11120 ~/Public/nih.gov

dcmqrti -c ~/dicom/db/dcmqrscp.cfg dcmtkCompany

all studies:

findscu -S -k "(0008,0052)=STUDY"  -aec DCMTK  localhost 11120

one patient:

findscu  -P -k "(0008,0052)=STUDY" -k "(0010,0020)=0522c0013"  -aec DCMTK  localhost 11120

one patient one accession:

findscu  -P -k "(0008,0052)=STUDY" -k "(0010,0020)=0522c0013" -k "(0008,0050)=2819497684894126" -aec DCMTK  localhost 11120

LifeImage standard 12,028 test set:

findscu  -P -k "(0008,0052)=STUDY" -k "(0010,0020)=AM-0502" -k "(0008,0050)=00002558" -aec DCMTK  localhost 11120


where dcmqrscp.cfg is defined as follows:

NetworkTCPPort  = 104

MaxPDUSize      = 16384

MaxAssociations = 16

HostTable BEGIN

dcmqrscp        = (dcmqrscp, stephens-mbp.lifeimage.lan, 11120)

dcmtkCompany    = dcmqrscp

HostTable END

VendorTable BEGIN

"dcmtk company" = dcmtkCompany

VendorTable END

AETable BEGIN

DCMTK        /Users/sbuck/dicom/db/DCMTK        RW (500, 1073741824) ANY

AETable END

Second dcmqrscp instance:  dcmqrscp -v -c ~/dicom2/db/dcmqrscp.cfg 11121

Send half of Willie to 11120 and the other half to 11121 to test multi-pacs Q/R:
dcmsend stephens-mbp.lifeimage.lan 11120 /Users/sbuck/Public/00011001.dcm   -v +v -nh -aec DCMTK
dcmsend stephens-mbp.lifeimage.lan 11121 /Users/sbuck/Public/00014001.dcm   -v +v -nh -aec DCMTK2

