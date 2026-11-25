#include "pch.h"
#include "CJwwReaderBase.h"
#include "CDataList.h"

CJwwReaderBase::CJwwReaderBase() {
}

CJwwReaderBase::~CJwwReaderBase() {

	{
		POSITION pos = m_DataList.GetHeadPosition();
		while (pos != NULL) {
			CData* data = m_DataList.GetNext(pos);
			delete data;
		}
	}
	{
		POSITION pos = m_DataListList.GetHeadPosition();
		while (pos != NULL) {
			CDataList* data = m_DataListList.GetNext(pos);
			delete data;
		}
	}
}

void CJwwReaderBase::Read(LPCTSTR path) {
	AFX_MANAGE_STATE(AfxGetStaticModuleState());
	m_DataList.RemoveAll();
	CFile file;
	// NewJwWeb: share read access (the default shareCompat is exclusive) and fail loudly instead of reading a closed file
	CFileException openError;
	if (!file.Open(path, CFile::modeRead | CFile::shareDenyWrite, &openError))
		AfxThrowFileException(openError.m_cause, openError.m_lOsError, path);
	CArchive ar(&file, CArchive::load);
	try {
		ReadFileType(ar);
		ReadHeader(ar);
		ReadData(ar);
		ReadDataList(ar);
		ReadImages(ar);
		ar.Close();
	}
	catch (...) {
		ar.Close();
		throw;
	}
}
void CJwwReaderBase::ReadData(CArchive& ar) {
	m_DataList.Serialize(ar);
}

void CJwwReaderBase::ReadDataList(CArchive& ar) {
	m_DataListList.Serialize(ar);
}

