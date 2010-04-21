using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{
    /* Stuff common to all the general-purpose Reed-Solomon codecs
    * Copyright 2004 Phil Karn, KA9Q
    * May be used under the terms of the GNU Lesser General Public License (LGPL)
    */
    /* The guts of the Reed-Solomon encoder, meant to be #included
     * into a function body with the following typedefs, macros and variables supplied
     * according to the code parameters:

     * data_t - a typedef for the data symbol
     * data_t data[] - array of NN-NROOTS-PAD and type data_t to be encoded
     * data_t parity[] - an array of NROOTS and type data_t to be written with parity symbols
     * NROOTS - the number of roots in the RS code generator polynomial,
     *          which is the same as the number of parity symbols in a block.
                Integer variable or literal.
            * 
     * NN - the total number of symbols in a RS block. Integer variable or literal.
     * PAD - the number of pad symbols in a block. Integer variable or literal.
     * ALPHA_TO - The address of an array of NN elements to convert Galois field
     *            elements in index (log) form to polynomial form. Read only.
     * INDEX_OF - The address of an array of NN elements to convert Galois field
     *            elements in polynomial form to index (log) form. Read only.
     * MODNN - a function to reduce its argument modulo NN. May be inline or a macro.
     * GENPOLY - an array of NROOTS+1 elements containing the generator polynomial in index form

     * The memset() and memmove() functions are used. The appropriate header
     * file declaring these functions (usually <string.h>) must be included by the calling
     * program.

     * Copyright 2004, Phil Karn, KA9Q
     * May be used under the terms of the GNU Lesser General Public License (LGPL)
     */


//typedef unsigned int data_t;

//#define MODNN(x) modnn(rs,x)

//#define MM (rs->mm)
//#define NN (rs->nn)
//#define ALPHA_TO (rs->alpha_to) 
//#define INDEX_OF (rs->index_of)
//#define GENPOLY (rs->genpoly)
//#define NROOTS (rs->nroots)
//#define FCR (rs->fcr)
//#define PRIM (rs->prim)
//#define IPRIM (rs->iprim)
//#define PAD (rs->pad)
//#define A0 (NN)


    class ReedSolomon
    {
        /* Reed-Solomon codec control block */
        int MM;             /* Bits per symbol */
        int NN;             /* Symbols per block (= (1<<mm)-1) */
        int[] ALPHA_TO;     /* log lookup table */
        int[] INDEX_OF;     /* Antilog lookup table */
        int[] GENPOLY;      /* Generator polynomial */
        int NROOTS;         /* Number of generator roots = number of parity symbols */
        int FCR;            /* First consecutive root, index form */
        int PRIM;           /* Primitive element, index form */
        int IPRIM;          /* prim-th root of 1, index form */
        int PAD;            /* Padding bytes in shortened block */
        int A0;


        int MODNN(int x)
        {
          while (x >= NN) {
            x -= NN;
            x = (x >> MM) + (x & NN);
          }
          return x;
        }

        /* Initialize a Reed-Solomon codec
         * symsize = symbol size, bits
         * gfpoly = Field generator polynomial coefficients
         * fcr = first root of RS code generator polynomial, index form
         * prim = primitive element to generate polynomial roots
         * nroots = RS code generator polynomial degree (number of roots)
         * pad = padding bytes at front of shortened block
         */
        public void Init(int symsize,int gfpoly,int fcr,int prim, int nroots,int pad)
        {
          int i, j, sr,root,iprim;

          /* Check parameter ranges */
          if(symsize < 0 || symsize > 8*sizeof(int))
            goto done;
          if(fcr < 0 || fcr >= (1<<symsize))
            goto done;
          if(prim <= 0 || prim >= (1<<symsize))
            goto done;
          if(nroots < 0 || nroots >= (1<<symsize))
            goto done; /* Can't have more roots than symbol values! */
          if(pad < 0 || pad >= ((1<<symsize) - 1 - nroots))
            goto done; /* Too much padding */

          MM = symsize;
          NN = (1<<symsize)-1;
          PAD = pad;
          A0 = NN;
          FCR = fcr;
          PRIM = prim;
          NROOTS = nroots;

          ALPHA_TO = new int[NN+1];
          INDEX_OF = new int[NN+1];
          GENPOLY = new int[(nroots+1)];

          /* Generate Galois field lookup tables */
          INDEX_OF[0] = A0; /* log(zero) = -inf */
          ALPHA_TO[A0] = 0; /* alpha**-inf = 0 */
          sr = 1;
          for(i=0;i<NN;i++)
          {
            INDEX_OF[sr] = i;
            ALPHA_TO[i] = sr;
            sr <<= 1;
            if((sr & (1<<symsize)) != 0 )
              sr ^= gfpoly;
            sr &= NN;
          }

          /* Form RS code generator polynomial from its roots */
          /* Find prim-th root of 1, used in decoding */
          for(iprim=1;(iprim % prim) != 0;iprim += NN)
            ;
          IPRIM = iprim / prim;

          GENPOLY[0] = 1;
          for (i = 0,root=fcr*prim; i < nroots; i++,root += prim) 
          {
            GENPOLY[i+1] = 1;
            /* Multiply rs->genpoly[] by  @**(root + x) */
            for (j = i; j > 0; j--)
            {
              if (GENPOLY[j] != 0)
	            GENPOLY[j] = GENPOLY[j-1] ^ ALPHA_TO[MODNN(INDEX_OF[GENPOLY[j]] + root)];
              else
	            GENPOLY[j] = GENPOLY[j-1];
            }
            /* rs->genpoly[0] can never be zero */
            GENPOLY[0] = ALPHA_TO[MODNN(INDEX_OF[GENPOLY[0]] + root)];
          }
          /* convert rs->genpoly[] to index form for quicker encoding */
          for (i = 0; i <= nroots; i++)
            GENPOLY[i] = INDEX_OF[GENPOLY[i]];
         done:;
        }

        public void Encode(int[] data, int[] parity)
        {
          int i, j;
          int feedback;
          Array.Clear(parity, 0, NROOTS); 

          for(i=0;i < NN-NROOTS-PAD;i++)
          {
            feedback = INDEX_OF[data[i] ^ parity[0]];
            if(feedback != A0)
	        {      /* feedback term is non-zero */
              for(j=1;j<NROOTS;j++)
			        parity[j] ^= ALPHA_TO[MODNN(feedback + GENPOLY[NROOTS-j])];
            }
            /* Shift */
            Array.Copy(parity, 1, parity, 0, NROOTS-1);
            if(feedback != A0)
              parity[NROOTS-1] = ALPHA_TO[MODNN(feedback + GENPOLY[0])];
            else
              parity[NROOTS-1] = 0;
          }
        }

        public int Decode(int[] data, int [] eras_pos, int no_eras)
        {
          int retval = 0;

          int deg_lambda, el, deg_omega;
          int i, j, r,k;
          int u,q,tmp,num1,num2,den,discr_r;
          int[] lambda, s;	/* Err+Eras Locator poly
					         * and syndrome poly */
          int [] b, t, omega;
          int [] root, reg, loc;
          int syn_error, count;
          
          lambda = new int[(NROOTS+1)];
          s = new int[(NROOTS)];
          b = new int[(NROOTS+1)];
          t = new int[(NROOTS+1)];
          omega = new int[(NROOTS+1)];
          root = new int[(NROOTS)];
          reg = new int[(NROOTS+1)];
          loc = new int[(NROOTS)];


          /* form the syndromes; i.e., evaluate data(x) at roots of g(x) */
          for(i=0;i<NROOTS;i++)
            s[i] = data[0];

          for(j=1;j<NN;j++)
          {
            int symbol = (j < (NN - PAD)) ? data[j] : 0; 
            for(i=0;i<NROOTS;i++)
            {
              if(s[i] == 0){
                  s[i] = symbol;
              } else {
                  s[i] = symbol ^ ALPHA_TO[MODNN(INDEX_OF[s[i]] + (FCR + i) * PRIM)];
              }
            }
          }

          /* Convert syndromes to index form, checking for nonzero condition */
          syn_error = 0;
          for(i=0;i<NROOTS;i++)
          {
            syn_error |= s[i];
            s[i] = INDEX_OF[s[i]];
          }

          if (syn_error == 0) 
          {
            /* if syndrome is zero, data[] is a codeword and there are no
             * errors to correct. So return data[] unmodified
             */
            retval = count = 0;
            goto finish;
          }
          Array.Clear(lambda,1,lambda.Length-1);
          // memset(&lambda[1],0,NROOTS*sizeof(lambda[0]));
          lambda[0] = 1;

          if (no_eras > 0) 
          {
            /* Init lambda to be the erasure locator polynomial */
            lambda[1] = ALPHA_TO[MODNN(PRIM*(NN-1-eras_pos[0]))];
            for (i = 1; i < no_eras; i++) 
            {
              u = MODNN(PRIM*(NN-1-eras_pos[i]));
              for (j = i+1; j > 0; j--) 
              {
	            tmp = INDEX_OF[lambda[j - 1]];
	            if(tmp != A0)
	              lambda[j] ^= ALPHA_TO[MODNN(u + tmp)];
              }
            }
          }
          for(i=0;i<NROOTS+1;i++)
            b[i] = INDEX_OF[lambda[i]];
  
          /*
           * Begin Berlekamp-Massey algorithm to determine error+erasure
           * locator polynomial
           */
          r = no_eras;
          el = no_eras;
          while (++r <= NROOTS) 
          {	/* r is the step number */
            /* Compute discrepancy at the r-th step in poly-form */
            discr_r = 0;
            for (i = 0; i < r; i++)
            {
              if ((lambda[i] != 0) && (s[r-i-1] != A0)) {
	            discr_r ^= ALPHA_TO[MODNN(INDEX_OF[lambda[i]] + s[r-i-1])];
              }
            }
            discr_r = INDEX_OF[discr_r];	/* Index form */
            if (discr_r == A0) 
            {
              /* 2 lines below: B(x) <-- x*B(x) */
              Array.Copy(b, 0, b, 1, NROOTS);
              //memmove(&b[1],b,NROOTS*sizeof(b[0]));
              b[0] = A0;
            } else 
            {
              /* 7 lines below: T(x) <-- lambda(x) - discr_r*x*b(x) */
              t[0] = lambda[0];
              for (i = 0 ; i < NROOTS; i++) 
              {
	            if(b[i] != A0)
	              t[i+1] = lambda[i+1] ^ ALPHA_TO[MODNN(discr_r + b[i])];
	            else
	              t[i+1] = lambda[i+1];
              }
              
              if (2 * el <= r + no_eras - 1) 
              {
	            el = r + no_eras - el;
	            /*
	             * 2 lines below: B(x) <-- inv(discr_r) *
	             * lambda(x)
	             */
	            for (i = 0; i <= NROOTS; i++)
	              b[i] = (lambda[i] == 0) ? A0 : MODNN(INDEX_OF[lambda[i]] - discr_r + NN);
              } else 
              {
	            /* 2 lines below: B(x) <-- x*B(x) */
	            Array.Copy(b, 0, b, 1, NROOTS);
                //memmove(&b[1],b,NROOTS*sizeof(b[0]));
	            b[0] = A0;
              }
              //memcpy(lambda,t,(NROOTS+1)*sizeof(t[0]));
              Array.Copy(t,0, lambda,0, (NROOTS+1));
            }
          }

          /* Convert lambda to index form and compute deg(lambda(x)) */
          deg_lambda = 0;
          for(i=0;i<NROOTS+1;i++)
          {
            lambda[i] = INDEX_OF[lambda[i]];
            if(lambda[i] != A0)
              deg_lambda = i;
          }
          /* Find roots of the error+erasure locator polynomial by Chien search */
          //memcpy(&reg[1],&lambda[1],NROOTS*sizeof(reg[0]));
          Array.Copy(lambda, 1, reg, 1, NROOTS);
          retval = count = 0;		/* Number of roots of lambda(x) */
          for (i = 1,k=IPRIM-1; i <= NN; i++,k = MODNN(k+IPRIM)) 
          {
            q = 1; /* lambda[0] is always 0 */
            for (j = deg_lambda; j > 0; j--)
            {
              if (reg[j] != A0) {
	            reg[j] = MODNN(reg[j] + j);
	            q ^= ALPHA_TO[reg[j]];
              }
            }
            if (q != 0)
              continue; /* Not a root */
            
            /* store root (index-form) and error location number */
            root[count] = i;
            loc[count] = k;
            /* If we've already found max possible roots,
             * abort the search to save time
             */
            if(++count == deg_lambda)
              break;
          }
          if (deg_lambda != count) 
          {
            /*
             * deg(lambda) unequal to number of roots => uncorrectable
             * error detected
             */
            retval = count = -1;
            goto finish;
          }
          /*
           * Compute err+eras evaluator poly omega(x) = s(x)*lambda(x) (modulo
           * x**NROOTS). in index form. Also find deg(omega).
           */
          deg_omega = deg_lambda-1;
          for (i = 0; i <= deg_omega;i++)
          {
            tmp = 0;
            for(j=i;j >= 0; j--)
            {
              if ((s[i - j] != A0) && (lambda[j] != A0))
	            tmp ^= ALPHA_TO[MODNN(s[i - j] + lambda[j])];
            }
            omega[i] = INDEX_OF[tmp];
          }

          /*
           * Compute error values in poly-form. num1 = omega(inv(X(l))), num2 =
           * inv(X(l))**(FCR-1) and den = lambda_pr(inv(X(l))) all in poly-form
           */
          for (j = count-1; j >=0; j--) 
          {
            num1 = 0;
            for (i = deg_omega; i >= 0; i--) 
            {
              if (omega[i] != A0)
	            num1  ^= ALPHA_TO[MODNN(omega[i] + i * root[j])];
            }
            num2 = ALPHA_TO[MODNN(root[j] * (FCR - 1) + NN)];
            den = 0;
            
            /* lambda[i+1] for i even is the formal derivative lambda_pr of lambda[i] */
            for (i = Math.Min(deg_lambda,NROOTS-1) & ~1; i >= 0; i -=2) 
            {
              if(lambda[i+1] != A0)
	            den ^= ALPHA_TO[MODNN(lambda[i+1] + i * root[j])];
            }

            /* Apply error to data */
            if ((num1 != 0) && (loc[j] < (NN - PAD))) 
            {
              data[loc[j]] ^= ALPHA_TO[MODNN(INDEX_OF[num1] + INDEX_OF[num2] + NN - INDEX_OF[den])];
            }
          }
         finish:
          if(eras_pos != null)
          {
            for(i=0;i<count;i++)
              eras_pos[i] = loc[i];
          }
          retval = count;
          return retval;
        }
    }
}
