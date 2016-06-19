﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoCADDataExtractor
{
  
    public class NZGD2000_NZTM
    {
        /* Defines PI (from Abramowitz and Stegun Table 1.1) */

        static double TWOPI = (2 * Math.PI);
        static double NZTM_A = 6378137;
        static double NZTM_RF = 298.257222101;

        static double NZTM_CM = 173.0;
        static double NZTM_OLAT = 0.0;
        static double NZTM_SF = 0.9996;
        static double NZTM_FE = 1600000.0;
        static double NZTM_FN = 10000000.0;

        public struct TMProjection
        {
            public double meridian;          /* Central meridian */
            public double scalef;            /* Scale factor */
            public double orglat;            /* Origin latitude */
            public double falsee;            /* False easting */
            public double falsen;            /* False northing */
            public double utom;              /* Unit to metre conversion */

            public double a, rf, f, e2, ep2;     /* Ellipsoid parameters */
            public double om;                /* Intermediate calculation */
        }

        /* Initiallize the TM structure  */
        static void TMProj(ref TMProjection tm, double a, double rf, double cm, double sf, double lto, double fe, double fn, double utom)
        {
            tm = new TMProjection();
            double f;

            tm.meridian = cm;
            tm.scalef = sf;
            tm.orglat = lto;
            tm.falsee = fe;
            tm.falsen = fn;
            tm.utom = utom;
            if (rf != 0.0)
                f = 1.0 / rf;
            else
                f = 0.0;
            tm.a = a;
            tm.rf = rf;
            tm.f = f;
            tm.e2 = (2.0 * f) - (f * f);
            tm.ep2 = tm.e2 / (1.0 - tm.e2);

            tm.om = meridian_arc(tm, tm.orglat);
        }

        /***************************************************************************/
        /*                                                                         */
        /*  meridian_arc                                                           */
        /*                                                                         */
        /*  Returns the length of meridional arc (Helmert formula)                 */
        /*  Method based on Redfearn's formulation as expressed in GDA technical   */
        /*  manual at http://www.anzlic.org.au/icsm/gdatm/index.html               */
        /*                                                                         */
        /*  Parameters are                                                         */
        /*    projection                                                           */
        /*    latitude (radians)                                                   */
        /*                                                                         */
        /*  Return value is the arc length in metres                               */
        /*                                                                         */
        /***************************************************************************/


        public static double meridian_arc(TMProjection tm, double lt)
        {
            double e2 = tm.e2;
            double a = tm.a;
            double e4;
            double e6;
            double A0;
            double A2;
            double A4;
            double A6;

            e4 = e2 * e2;
            e6 = e4 * e2;

            A0 = 1 - (e2 / 4.0) - (3.0 * e4 / 64.0) - (5.0 * e6 / 256.0);
            A2 = (3.0 / 8.0) * (e2 + e4 / 4.0 + 15.0 * e6 / 128.0);
            A4 = (15.0 / 256.0) * (e4 + 3.0 * e6 / 4.0);
            A6 = 35.0 * e6 / 3072.0;

            return a * (A0 * lt - A2 * Math.Sin(2 * lt) + A4 * Math.Sin(4 * lt) - A6 * Math.Sin(6 * lt));
        }

        /*************************************************************************/
        /*                                                                       */
        /*   foot_point_lat                                                      */
        /*                                                                       */
        /*   Calculates the foot point latitude from the meridional arc          */
        /*   Method based on Redfearn's formulation as expressed in GDA technical*/
        /*   manual at http://www.anzlic.org.au/icsm/gdatm/index.html            */
        /*                                                                       */
        /*   Takes parameters                                                    */
        /*      tm definition (for scale factor)                                 */
        /*      meridional arc (metres)                                          */
        /*                                                                       */
        /*   Returns the foot point latitude (radians)                           */
        /*                                                                       */
        /*************************************************************************/


        static double foot_point_lat(TMProjection tm, double m)
        {
            double f = tm.f;
            double a = tm.a;
            double n;
            double n2;
            double n3;
            double n4;
            double g;
            double sig;
            double phio;

            n = f / (2.0 - f);
            n2 = n * n;
            n3 = n2 * n;
            n4 = n2 * n2;

            g = a * (1.0 - n) * (1.0 - n2) * (1 + 9.0 * n2 / 4.0 + 225.0 * n4 / 64.0);
            sig = m / g;

            phio = sig + (3.0 * n / 2.0 - 27.0 * n3 / 32.0) * Math.Sin(2.0 * sig)
                            + (21.0 * n2 / 16.0 - 55.0 * n4 / 32.0) * Math.Sin(4.0 * sig)
                            + (151.0 * n3 / 96.0) * Math.Sin(6.0 * sig)
                            + (1097.0 * n4 / 512.0) * Math.Sin(8.0 * sig);

            return phio;
        }

        /***************************************************************************/
        /*                                                                         */
        /*   tmgeod                                                                */
        /*                                                                         */
        /*   Routine to convert from Tranverse Mercator to latitude and longitude. */
        /*   Method based on Redfearn's formulation as expressed in GDA technical  */
        /*   manual at http://www.anzlic.org.au/icsm/gdatm/index.html              */
        /*                                                                         */
        /*   Takes parameters                                                      */
        /*      input easting (metres)                                             */
        /*      input northing (metres)                                            */
        /*      output latitude (radians)                                          */
        /*      output longitude (radians)                                         */
        /*                                                                         */
        /***************************************************************************/

        static void tm_geod(TMProjection tm,
                      double ce, double cn, ref double ln, ref double lt)
        {
            double fn = tm.falsen;
            double fe = tm.falsee;
            double sf = tm.scalef;
            double e2 = tm.e2;
            double a = tm.a;
            double cm = tm.meridian;
            double om = tm.om;
            double utom = tm.utom;
            double cn1;
            double fphi;
            double slt;
            double clt;
            double eslt;
            double eta;
            double rho;
            double psi;
            double E;
            double x;
            double x2;
            double t;
            double t2;
            double t4;
            double trm1;
            double trm2;
            double trm3;
            double trm4;

            cn1 = (cn - fn) * utom / sf + om;
            fphi = foot_point_lat(tm, cn1);
            slt = Math.Sin(fphi);
            clt = Math.Cos(fphi);

            eslt = (1.0 - e2 * slt * slt);
            eta = a / Math.Sqrt(eslt);
            rho = eta * (1.0 - e2) / eslt;
            psi = eta / rho;

            E = (ce - fe) * utom;
            x = E / (eta * sf);
            x2 = x * x;


            t = slt / clt;
            t2 = t * t;
            t4 = t2 * t2;

            trm1 = 1.0 / 2.0;

            trm2 = ((-4.0 * psi
                         + 9.0 * (1 - t2)) * psi
                         + 12.0 * t2) / 24.0;

            trm3 = ((((8.0 * (11.0 - 24.0 * t2) * psi
                          - 12.0 * (21.0 - 71.0 * t2)) * psi
                          + 15.0 * ((15.0 * t2 - 98.0) * t2 + 15)) * psi
                          + 180.0 * ((-3.0 * t2 + 5.0) * t2)) * psi + 360.0 * t4) / 720.0;

            trm4 = (((1575.0 * t2 + 4095.0) * t2 + 3633.0) * t2 + 1385.0) / 40320.0;

            lt = fphi + (t * x * E / (sf * rho)) * (((trm4 * x2 - trm3) * x2 + trm2) * x2 - trm1);

            trm1 = 1.0;

            trm2 = (psi + 2.0 * t2) / 6.0;

            trm3 = (((-4.0 * (1.0 - 6.0 * t2) * psi
                       + (9.0 - 68.0 * t2)) * psi
                       + 72.0 * t2) * psi
                       + 24.0 * t4) / 120.0;

            trm4 = (((720.0 * t2 + 1320.0) * t2 + 662.0) * t2 + 61.0) / 5040.0;

            ln = cm - (x / clt) * (((trm4 * x2 - trm3) * x2 + trm2) * x2 - trm1);
        }


        /***************************************************************************/
        /*                                                                         */
        /*   geodtm                                                                */
        /*                                                                         */
        /*   Routine to convert from latitude and longitude to Transverse Mercator.*/
        /*   Method based on Redfearn's formulation as expressed in GDA technical  */
        /*   manual at http://www.anzlic.org.au/icsm/gdatm/index.html              */
        /*   Loosely based on FORTRAN source code by J.Hannah and A.Broadhurst.    */
        /*                                                                         */
        /*   Takes parameters                                                      */
        /*      input latitude (radians)                                           */
        /*      input longitude (radians)                                          */
        /*      output easting  (metres)                                           */
        /*      output northing (metres)                                           */
        /*                                                                         */
        /***************************************************************************/


        static void geod_tm(TMProjection tm,
                      double ln, double lt, ref double ce, ref double cn)
        {
            double fn = tm.falsen;
            double fe = tm.falsee;
            double sf = tm.scalef;
            double e2 = tm.e2;
            double a = tm.a;
            double cm = tm.meridian;
            double om = tm.om;
            double utom = tm.utom;
            double dlon;
            double m;
            double slt;
            double eslt;
            double eta;
            double rho;
            double psi;
            double clt;
            double w;
            double wc;
            double wc2;
            double t;
            double t2;
            double t4;
            double t6;
            double trm1;
            double trm2;
            double trm3;
            double gce;
            double trm4;
            double gcn;

            dlon = ln - cm;
            while (dlon > Math.PI) dlon -= TWOPI;
            while (dlon < -Math.PI) dlon += TWOPI;

            m = meridian_arc(tm, lt);

            slt = Math.Sin(lt);

            eslt = (1.0 - e2 * slt * slt);
            eta = a / Math.Sqrt(eslt);
            rho = eta * (1.0 - e2) / eslt;
            psi = eta / rho;

            clt = Math.Cos(lt);
            w = dlon;

            wc = clt * w;
            wc2 = wc * wc;

            t = slt / clt;
            t2 = t * t;
            t4 = t2 * t2;
            t6 = t2 * t4;

            trm1 = (psi - t2) / 6.0;

            trm2 = (((4.0 * (1.0 - 6.0 * t2) * psi
                          + (1.0 + 8.0 * t2)) * psi
                          - 2.0 * t2) * psi + t4) / 120.0;

            trm3 = (61 - 479.0 * t2 + 179.0 * t4 - t6) / 5040.0;

            gce = (sf * eta * dlon * clt) * (((trm3 * wc2 + trm2) * wc2 + trm1) * wc2 + 1.0);
            ce = gce / utom + fe;

            trm1 = 1.0 / 2.0;

            trm2 = ((4.0 * psi + 1) * psi - t2) / 24.0;

            trm3 = ((((8.0 * (11.0 - 24.0 * t2) * psi
                        - 28.0 * (1.0 - 6.0 * t2)) * psi
                        + (1.0 - 32.0 * t2)) * psi
                        - 2.0 * t2) * psi
                        + t4) / 720.0;

            trm4 = (1385.0 - 3111.0 * t2 + 543.0 * t4 - t6) / 40320.0;

            gcn = (eta * t) * ((((trm4 * wc2 + trm3) * wc2 + trm2) * wc2 + trm1) * wc2);
            cn = (gcn + m - om) * sf / utom + fn;

            return;
        }

        /* Define a static implementation of tmprojection */
        /* Note: for some implementations it may be better to create this
           dynamically and develop modified versions of the transformation
           functions to take this as a parameter */

        static bool initiallized = false;
        static TMProjection projection;
        static TMProjection Get_NZTM_Projection()
        {
            if (!initiallized)
            {
                projection = new TMProjection();
                TMProj(ref projection, NZTM_A, NZTM_RF, AngleConvertion.Deg2Rad(NZTM_CM), NZTM_SF, AngleConvertion.Deg2Rad(NZTM_OLAT), NZTM_FE, NZTM_FN, 1.0);
                initiallized = true;
            }
            return projection;
        }

        /* Functions implementation the TM projection specifically for the
           NZTM coordinate system
        */

        public static void nztm_geod(double n, double e, ref double lt, ref double ln)
        {
            TMProjection nztm = Get_NZTM_Projection();
            tm_geod(nztm, e, n, ref ln, ref lt);
        }

        public static void geod_nztm(double lt, double ln, ref double n, ref double e)
        {
            TMProjection nztm = Get_NZTM_Projection();
            geod_tm(nztm, ln, lt, ref e, ref n);
        }
    }




    public static class AngleConvertion
    {
        public static double Deg2Rad(double value)
        {
            double radians = value * (Math.PI / 180);
            return radians;
        }

        public static double Rad2Deg(double value)
        {
            double degrees = value * (180 * Math.PI);
            return degrees;
        }

    }
}