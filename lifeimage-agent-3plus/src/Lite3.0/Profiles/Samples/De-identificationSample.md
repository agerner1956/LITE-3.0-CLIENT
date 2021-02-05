# The De-identification Sample profile demonstrates several tag-level scripts used to alter or remove tag data as needed for the purposes of de-identification.  The future goal of this sample is also to include a script capable of blanking out any data in the image itself within the industry standard ranges for embedded in images, as well as to remove overlays that may contain identifying information.

AgeAtExam - Populates the age of the patient at the time of the exam.

Hello World - The default script example.

LI-GENERATEDNUMBER - Attempts to provide a sequential number to replace the specified tag such as patient id and name.  LifeImage transfer exchange is for the most part a stateless actor, so the script needs to call out to something that maintains a persistent cross-reference table of LI-GENERATEDNUMBER. The risk of using something like this involves the need to create a lookup table used to replace the fields of a given study uid or patient id with the generated number, putting the concept of de-identification at risk because someone could just look up the value in the table and translate.

Minus1Month - To assist with de-identification while preserving some relative date of event, this script subtracts a date field by 1 month.

Minus1Hour - To assist with de-identification while preserving some relative time of event, this script subtracts a time field by 1 hour.

PrefixWithIM - Prefixes the designated field value with "IM", such as Accession Number in the example. 

RandomizeString - Randomizes a specified DICOM tag of datatype string for the full length of a field.  This method operates independently on each image within a study to ensure there is no trail left behind besides the study and series uids that groups the images into a set.

RemoveTag - Removes the specified tag, data and all.

SiteNumber - Sets the value of the specified tag to the "SiteNumberGoesHere" value you supply in the script. 

Unisex - Changes the patient sex to O.

WhiteList - Removes all tags not specified by the rule.  If you want to keep a tag but not modify it, specify the tag with an appropriate filter tag such as "null or empty value."

YES - Sets the value of the specified tag to YES.

