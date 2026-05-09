LICENSE

	In order for the NdmAkiyama module to work you have to place the Akiyama .lic license in
	the same folder as the NdmAkiyama.dll.

EXTENDED AKIYAMA SDK (Crop & Segment)

	In order to use Akiyama image processing you have to place these files in the same folder
	where your application is opened from:
		* aw_preface.dll (Crop)
		* libak_cropper.dll (Crop)
		* redist folder and inside the folder - FaceModelStandard.dat (Crop)
		* libak_segmenter.dll (Segment)
		* model.pm (Segment)
		* segm.dll (Segment)
		* tensorflow.dll (Segment)

	Also you have to place the .xml profile in the same folder as the NdmAkiyama.dll. (Crop)
	
	After that you can set Crop and Segmentation through the "AkiyamaImageProcessing" parameter.