package ModelDownloader {
    private import SoftwareItemMetadata::*;
    private import ModelDownloader_Program::*;

    doc
    /*
     * ModelDownloader is the non-shipped, CI-only console tool that provisions real speech models into the per-user
     * model store before the integration tests run.
     */

    part def ModelDownloaderSystem {
        doc /* The non-shipped, CI-only ModelDownloader console tool as a whole (no design, verification or
             * requirements documents of its own). */

        metadata info : SoftwareInfo {
        }

        part program : Program;
    }
}
