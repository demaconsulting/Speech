## Pandoc Verification

This document provides the verification evidence for the `Pandoc` OTS software item.

### Required Functionality

DemaConsulting.PandocTool converts Markdown source documents to HTML as part of the documentation
build pipeline. FileAssert validates that each generated HTML file exists, contains a valid HTML
title element, and includes expected document content. Passing FileAssert assertions for each
document type proves Pandoc executed correctly and produced meaningful output.

### Verification Approach

Pandoc is verified by self-validation evidence from the CI pipeline. Each scenario is a FileAssert
assertion that runs after Pandoc converts a specific Markdown document to HTML. A passing pipeline
run for all scenarios constitutes evidence that the requirement is satisfied.

### Test Scenarios

#### Pandoc_BuildNotesHtml

**Scenario**: FileAssert asserts the build-notes HTML file exists, contains a valid HTML title
element, and includes expected document content.

**Expected**: FileAssert exits 0 for the build-notes HTML document.

#### Pandoc_CodeQualityHtml

**Scenario**: FileAssert asserts the code-quality HTML file exists, contains a valid HTML title
element, and includes expected document content.

**Expected**: FileAssert exits 0 for the code-quality HTML document.

#### Pandoc_ReviewPlanHtml

**Scenario**: FileAssert asserts the review plan HTML file exists, contains a valid HTML title
element, and includes expected document content.

**Expected**: FileAssert exits 0 for the review plan HTML document.

#### Pandoc_ReviewReportHtml

**Scenario**: FileAssert asserts the review report HTML file exists, contains a valid HTML title
element, and includes expected document content.

**Expected**: FileAssert exits 0 for the review report HTML document.

#### Pandoc_DesignHtml

**Scenario**: FileAssert asserts the design document HTML file exists, contains a valid HTML title
element, and includes expected document content.

**Expected**: FileAssert exits 0 for the design document HTML.

#### Pandoc_VerificationHtml

**Scenario**: FileAssert asserts the verification HTML file exists, contains a valid HTML title
element, and includes expected verification document content.

**Expected**: FileAssert exits 0 for the verification document.

#### Pandoc_UserGuideHtml

**Scenario**: FileAssert asserts the user guide HTML file exists, contains a valid HTML title
element, and includes expected document content.

**Expected**: FileAssert exits 0 for the user guide HTML document.
