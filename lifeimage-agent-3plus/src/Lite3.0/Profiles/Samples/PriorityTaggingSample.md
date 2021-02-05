# Priority Tagging Sample

The Priority Tagging Sample Profile demonstrates the ability to apply values to "0000,0700" Priority based upon the inbound DICOMConnection.  The presence of Priority values High and Medium have the effect of throttling lower priority image transfers. Note that each priority is assigned to a different DICOMConnection.  Other possibilities exist such as assigning by received AETitle used during the connection request, or any other tag or set of tags that might be used to imply urgency. 

LowPriority - Assigns 0x2

MediumPriority - Assigns 0x0

HighPriority - Assigns 0x1
