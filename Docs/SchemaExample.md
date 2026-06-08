Collection	Property	DataType	IsMandatory	source	flow	flowkey	jsonpath
usrRequestBasicInfo	requestCode	text	TRUE	compute	publish	requestCode	requestCode
usrRequestBasicInfo	moduleCode	text	TRUE	compute			moduleCode
usrRequestBasicInfo	statusID	text	TRUE	compute			statusID
usrRequestBasicInfo	createdOn	datetime	TRUE	compute			createdOn
usrRequestBasicInfo	excelFilename	text	TRUE	compute			excelFilename
usrRequestBasicInfo	rowNumber	integer	TRUE	compute			rowNumber
usrRequestBasicInfo	_id	objectid	TRUE	auto	publish	requestId	_id
masterDesignations	designationCode	text	TRUE	excel	publish	designationCode	designations.designationCode
masterDesignations	designationName	text	TRUE	excel	publish	designationName	designations.designationName
masterDesignations	formattedName	text	TRUE	compute	publish	designationFormattedName	designations.formattedName
masterDesignations	requestCode	text	TRUE	compute	consume	requestCode	systemData.requestCode
masterDesignations	moduleCode	text	TRUE	compute			systemData.moduleCode
masterDesignations	formattedReferenceNumber	text	TRUE	compute			systemData.formattedReferenceNumber
masterDesignations	referenceNumber	text	TRUE	compute			systemData.referenceNumber
masterDesignations	createdOn	datetime	TRUE	compute			systemData.createdOn
masterDesignations	updatedOn	datetime	TRUE	compute			systemData.updatedOn
masterDesignations	basicInfoID	objectid	TRUE	compute	consume	requestId	systemData.basicInfoID
masterDesignations	statusID	objectid	TRUE	compute			systemData.statusID
masterDesignations	_id	objectid	TRUE	auto	publish	designationId	_id
masterDesignations	excelFilename	text	TRUE	compute			excelFilename
masterDesignations	rowNumber	integer	TRUE	compute			rowNumber
masterDesignations	actualID	objectid	TRUE	update			systemData.actualID
masterUsers	userLoginID	text	TRUE	excel			userDetails.userLoginID
masterUsers	employeeID	text	TRUE	excel			userDetails.employeeID
masterUsers	userName	text	TRUE	excel			userDetails.userName
masterUsers	gender	object	FALSE	compute			userDetails.gender
masterUsers	dateOfJoining	text	FALSE	excel			userDetails.dateOfJoining
masterUsers	officialEmail	text	FALSE	excel			userDetails.officialEmail
masterUsers	formattedName	text	TRUE	compute			userDetails.formattedName
masterUsers	itemID	text	TRUE	compute	consume	designationId	userDetails.designation.itemID
masterUsers	itemActualID	text	TRUE	compute	consume	designationId	userDetails.designation.itemActualID
masterUsers	itemCode	text	TRUE	compute	consume	designationCode	userDetails.designation.itemCode
masterUsers	item	text	TRUE	compute	consume	designationName	userDetails.designation.item
masterUsers	displayData	text	TRUE	compute	consume	designationFormattedName	userDetails.designation.displayData
masterUsers	requestCode	text	TRUE	compute	consume	requestCode	systemData.requestCode
masterUsers	moduleCode	text	TRUE	compute			systemData.moduleCode
masterUsers	formattedReferenceNumber	text	TRUE	compute			systemData.formattedReferenceNumber
masterUsers	referenceNumber	text	TRUE	compute			systemData.referenceNumber
masterUsers	createdOn	datetime	TRUE	compute			systemData.createdOn
masterUsers	updatedOn	datetime	TRUE	compute			systemData.updatedOn
masterUsers	basicInfoID	objectid	TRUE	compute	consume	requestId	systemData.basicInfoID
masterUsers	statusID	objectid	TRUE	compute			systemData.statusID
masterUsers	excelFilename	text	TRUE	compute			excelFilename
masterUsers	rowNumber	integer	TRUE	compute			rowNumber
masterUsers	actualID	objectid	TRUE	update			systemData.actualID

Data Excel
designationCode	designationName	employeeID	userName	gender	dateOfJoining	officialEmail	userLoginID


Json paths
_id
isSynced
excelFilename
rowNumber

requestCode
moduleCode
createdOn

systemData.requestCode
systemData.createdOn
systemData.statusID
designations.designationName
designations.designationCode

userDetails.designation.itemID
userDetails.designation.itemCode
userDetails.designation.item
userDetails.designation.displayData



requestCode
    Published By:
        usrRequestBasicInfo.requestCode
    Consumed By:
        masterDesignations.requestCode
        masterUsers.requestCode

designationId
    Published By:
        masterDesignations._id
    Consumed By:
        masterUsers.userDetails.designation.itemID

designationName
    Published By:
        masterDesignations.designations.designationName
    Consumed By:
        masterUsers.userDetails.designation.item