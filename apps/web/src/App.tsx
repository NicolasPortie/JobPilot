import { AppLayout } from './components/AppLayout'
import { useJobPilotApp } from './app/useJobPilotApp'
import { LoginScreen } from './features/auth/LoginScreen'
import { ApplicationsTab } from './features/applications/ApplicationsTab'
import { ProfileTab } from './features/profile/ProfileTab'
import { ResumeAtsTab } from './features/resume/ResumeAtsTab'
import { SettingsTab } from './features/settings/SettingsTab'
import './index.css'

function App() {
  const {
    token, email, setEmail, password, setPassword, loginError, loginBusy, refreshBusy, globalNotice,
    tab, setTab, profile, setProfile, recommendedSkillAdditions,
    validationIssues, profileCompletion,
    resumeDocuments, selectedResumeVersionId, setSelectedResumeVersionId, atsJobDescription, setAtsJobDescription,
    atsJobUrl, setAtsJobUrl, atsScreenshotFile, setAtsScreenshotFile, atsResumeBusy,
    atsTailoredResume, atsJobTarget, atsBatchItems, appliedVersionIds, atsUseAi, setAtsUseAi, atsExportBusy, atsResumeNotice, resumeLayouts,
    selectedLayout, setSelectedLayout,
    profileImportBusy, profileImportResult,
    profileImportNotice, profileNotice, profileSaveBusy, selectedResumeFile, setSelectedResumeFile,
    login, logout, saveProfile, deleteProfile, deleteAccount, retryDanger, retention, dangerNotice, dangerBusy,
    removeRecommendedSkillAddition, importResumeFile, buildAtsResume, buildBaseResume,
    exportAtsResume, downloadVersion, markApplied, startNextJob, saveReviewAnswer, regenerateAfterAnswers, updateTailoredSection, addProfileSkill,
    refresh,
  } = useJobPilotApp()

  const resumeMeta = {
    userName: profile.fullName,
    userRole: profile.targetRole || profile.seniority || 'JobPilot',
    completion: profileCompletion,
  }

  if (!token) {
    return (
      <LoginScreen
        email={email}
        password={password}
        error={loginError}
        busy={loginBusy}
        setEmail={setEmail}
        setPassword={setPassword}
        onLogin={login}
      />
    )
  }

  return (
    <AppLayout tab={tab} setTab={setTab} onRefresh={refresh} onLogout={logout} notice={globalNotice} refreshing={refreshBusy} resumeMeta={resumeMeta}>
      {tab === 'profile' && (
        <ProfileTab
          profile={profile}
          setProfile={setProfile}
          onSave={saveProfile}
          saveBusy={profileSaveBusy}
          notice={profileNotice}
          onRemoveRecommendedSkillAddition={removeRecommendedSkillAddition}
          recommendedSkillAdditions={recommendedSkillAdditions}
          onImportResumeFile={importResumeFile}
          selectedResumeFile={selectedResumeFile}
          setSelectedResumeFile={setSelectedResumeFile}
          importBusy={profileImportBusy}
          importResult={profileImportResult}
          importNotice={profileImportNotice}
          validationIssues={validationIssues}
          onDeleteProfile={deleteProfile}
          onDeleteAccount={deleteAccount}
          onRetryDanger={retryDanger}
          retention={retention}
          dangerNotice={dangerNotice}
          dangerBusy={dangerBusy}
        />
      )}
      {tab === 'resume' && (
        <ResumeAtsTab
          profile={profile}
          validationIssues={validationIssues}
          documents={resumeDocuments}
          selectedVersionId={selectedResumeVersionId}
          onSelectVersion={setSelectedResumeVersionId}
          jobDescription={atsJobDescription}
          setJobDescription={setAtsJobDescription}
          jobUrl={atsJobUrl}
          setJobUrl={setAtsJobUrl}
          screenshotFile={atsScreenshotFile}
          setScreenshotFile={setAtsScreenshotFile}
          draft={atsTailoredResume}
          jobTarget={atsJobTarget}
          busy={atsResumeBusy}
          exportBusy={atsExportBusy}
          notice={atsResumeNotice}
          useAi={atsUseAi}
          onToggleUseAi={setAtsUseAi}
          onBuild={buildAtsResume}
          onBuildBase={buildBaseResume}
          onExport={exportAtsResume}
          setTab={setTab}
          layoutOptions={resumeLayouts}
          selectedLayout={selectedLayout}
          setSelectedLayout={setSelectedLayout}
          onRegenerateAfterAnswers={regenerateAfterAnswers}
          onSaveReviewAnswer={saveReviewAnswer}
          onAddProfileSkill={addProfileSkill}
          onUpdateTailoredSection={updateTailoredSection}
          batchItems={atsBatchItems}
          appliedVersionIds={appliedVersionIds}
          onDownloadVersion={(versionId, format) => void downloadVersion(versionId, format)}
          onMarkApplied={(versionId, applied) => void markApplied(versionId, applied)}
          onNextJob={startNextJob}
        />
      )}
      {tab === 'settings' && <SettingsTab />}
      {tab === 'applications' && (
        <ApplicationsTab
          exportBusy={atsExportBusy}
          onDownload={(versionId, format) => void downloadVersion(versionId, format)}
          onMarkApplied={markApplied}
          onGoToResume={() => setTab('resume')}
        />
      )}
    </AppLayout>
  )
}

export default App
