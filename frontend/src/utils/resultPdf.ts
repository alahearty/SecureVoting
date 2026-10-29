import type { ElectionResult } from '@/api/types'
import { formatDateTime, formatPercent } from './format'

export async function exportResultPdf(result: ElectionResult): Promise<void> {
  const { jsPDF } = await import('jspdf')
  const doc = new jsPDF({ unit: 'pt', format: 'a4' })
  const pageWidth = doc.internal.pageSize.getWidth()
  const margin = 48
  let y = 54

  doc.setFillColor(11, 93, 59)
  doc.rect(0, 0, pageWidth, 88, 'F')
  doc.setTextColor(255, 255, 255)
  doc.setFont('helvetica', 'bold')
  doc.setFontSize(20)
  doc.text('IAUE SecureVote', margin, 38)
  doc.setFontSize(11)
  doc.setFont('helvetica', 'normal')
  doc.text('Official departmental election result', margin, 58)
  doc.text(`Version ${result.version} · ${result.status}`, pageWidth - margin, 38, { align: 'right' })
  doc.text(formatDateTime(result.publishedAt ?? result.generatedAt), pageWidth - margin, 58, { align: 'right' })

  y = 116
  doc.setTextColor(18, 32, 25)
  doc.setFont('helvetica', 'bold')
  doc.setFontSize(16)
  doc.text(result.electionTitle, margin, y, { maxWidth: pageWidth - margin * 2 })
  y += 28
  doc.setFont('helvetica', 'normal')
  doc.setFontSize(10)
  doc.text(
    `Eligible voters: ${result.totalEligible}    Accepted ballots: ${result.totalBallots}    Turnout: ${formatPercent(result.turnoutPercentage)}`,
    margin,
    y
  )
  y += 24

  for (const position of result.positions) {
    if (y > 730) {
      doc.addPage()
      y = 50
    }

    doc.setFont('helvetica', 'bold')
    doc.setFontSize(12)
    doc.text(position.positionTitle, margin, y)
    y += 6
    doc.setFont('helvetica', 'normal')
    doc.setFontSize(9)
    doc.setTextColor(104, 119, 110)
    doc.text(`${position.seatCount} seat(s) · ${position.abstentions} abstention(s)`, margin, y + 8)
    y += 20
    doc.setTextColor(18, 32, 25)

    for (const candidate of position.candidates) {
      const marker = candidate.isWinner ? 'WINNER' : candidate.isTie ? 'TIE' : ''
      doc.text(candidate.candidateName, margin, y)
      doc.text(`${candidate.votes} (${formatPercent(candidate.percentage)})`, 380, y, { align: 'right' })
      if (marker) {
        doc.setTextColor(candidate.isWinner ? 24 : 168, candidate.isWinner ? 122 : 92, candidate.isWinner ? 82 : 28)
        doc.setFont('helvetica', 'bold')
        doc.text(marker, pageWidth - margin, y, { align: 'right' })
        doc.setFont('helvetica', 'normal')
        doc.setTextColor(18, 32, 25)
      }
      y += 15
    }

    if (position.tieNote) {
      doc.setTextColor(168, 122, 28)
      doc.text(position.tieNote, margin, y)
      doc.setTextColor(18, 32, 25)
      y += 15
    }

    y += 10
  }

  if (y > 700) {
    doc.addPage()
    y = 50
  }

  doc.setFontSize(8)
  doc.setTextColor(104, 119, 110)
  doc.text(`Result integrity hash: ${result.integrityHash}`, margin, y)
  doc.text('This PDF is an aggregate report. It does not reveal any voter-to-choice mapping.', margin, y + 12)
  doc.save(`securevote-result-v${result.version}.pdf`)
}
